[CmdletBinding()]
param(
    [string]$EvidenceDir = "",
    [string]$ToolsDir = "",
    [Parameter(Mandatory = $true)]
    [string]$RunId,
    [string]$CommittedSummaryPath = "",
    [switch]$ValidateExisting
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item $PSScriptRoot\..).FullName

if ([string]::IsNullOrWhiteSpace($RunId)) {
    throw "RunId is required. Scanner reports must be bound to the owning quality-gate run."
}

if (-not $ToolsDir) {
    $ToolsDir = Join-Path $repoRoot ".tools\security"
}
$ToolsDir = [System.IO.Path]::GetFullPath($ToolsDir)

if (-not $EvidenceDir) {
    $evidenceFullDir = Join-Path $repoRoot "artifacts\quality-gate\run-$RunId"
} elseif ([System.IO.Path]::IsPathRooted($EvidenceDir)) {
    $evidenceFullDir = [System.IO.Path]::GetFullPath($EvidenceDir)
} else {
    $evidenceFullDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $EvidenceDir))
}

$securityDir = Join-Path $repoRoot "artifacts\security"
if (-not (Test-Path $securityDir)) {
    [void](New-Item -ItemType Directory -Path $securityDir -Force)
}

if (-not $CommittedSummaryPath) {
    $CommittedSummaryPath = Join-Path $securityDir "security-summary.json"
} elseif (-not [System.IO.Path]::IsPathRooted($CommittedSummaryPath)) {
    $CommittedSummaryPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $CommittedSummaryPath))
}

$manifestPath = Join-Path $PSScriptRoot "security-tools.json"
if (-not (Test-Path $manifestPath)) {
    throw "Security manifest not found at: $manifestPath"
}
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

$platform = "windows-x64"
$exeExt = ".exe"
if ($IsLinux) {
    $platform = "linux-x64"
    $exeExt = ""
}

function Protect-MachinePaths([string]$text) {
    if ([string]::IsNullOrEmpty($text)) { return $text }
    $t = $text -replace 'checksum=sha256%3A', 'checksum=sha256:'
    $t = $t -replace 'sha256%3A', 'sha256:'
    $t = $t -replace '(?i)[A-Z]:\\Users\\[^\s"]+', '<USER_PROFILE>'
    $t = $t -replace '(?i)/Users/[^/\s"]+', '<USER_PROFILE>'
    $t = $t -replace '(?i)/home/[^/\s"]+', '<USER_PROFILE>'
    $t = $t -replace '(?i)[A-Z]:\\[^\s"]*AppData[^\s"]*', '<USER_CACHE>'
    $t = $t -replace '(?i)OneDrive', '<CLOUD_HOME>'
    return $t
}

function Get-FileSha256Upper([string]$Path) {
    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToUpperInvariant()
}

function Assert-RunSidecar {
    param(
        [string]$ArtifactPath,
        [string]$ExpectedScanner
    )
    $metaPath = "$ArtifactPath.meta.json"
    if (-not (Test-Path $metaPath)) {
        throw "Missing run sidecar for '$ExpectedScanner' at '$metaPath'."
    }
    $meta = Get-Content $metaPath -Raw | ConvertFrom-Json
    if ($meta.runId -ne $RunId) {
        throw "Stale/cross-run sidecar for '$ExpectedScanner': sidecar runId '$($meta.runId)' != required '$RunId'."
    }
    if ($meta.scanner -ne $ExpectedScanner) {
        throw "Sidecar scanner mismatch for '$ArtifactPath': expected '$ExpectedScanner', found '$($meta.scanner)'."
    }
    $actualHash = Get-FileSha256Upper $ArtifactPath
    if ($meta.sha256.ToUpperInvariant() -ne $actualHash) {
        throw "Stale sidecar hash for '$ExpectedScanner': sidecar '$($meta.sha256)' != file '$actualHash'."
    }
    return $actualHash
}

function Set-AtomicFileContent {
    param(
        [string]$Destination,
        [string]$Content
    )
    $destDir = Split-Path -Parent $Destination
    if (-not (Test-Path $destDir)) {
        [void](New-Item -ItemType Directory -Path $destDir -Force)
    }
    $tmp = Join-Path $destDir (".tmp-" + [guid]::NewGuid().ToString('N'))
    $bak = Join-Path $destDir (".bak-" + [guid]::NewGuid().ToString('N'))
    $utf8 = [System.Text.UTF8Encoding]::new($false)
    [System.IO.File]::WriteAllText($tmp, $Content, $utf8)
    try {
        if (Test-Path $Destination) {
            [System.IO.File]::Replace($tmp, $Destination, $bak)
            if (Test-Path $bak) {
                Remove-Item -Path $bak -Force
            }
        } else {
            [System.IO.File]::Move($tmp, $Destination)
        }
    } catch {
        if ((Test-Path $bak) -and -not (Test-Path $Destination)) {
            [System.IO.File]::Move($bak, $Destination)
        } elseif (Test-Path $bak) {
            [System.IO.File]::Replace($bak, $Destination, $tmp + ".failed")
        }
        if (Test-Path $tmp) {
            Remove-Item -Path $tmp -Force
        }
        throw
    }
}

function Get-StrictToolInfo([string]$Name) {
    $toolDef = $manifest.tools | Where-Object { $_.name -eq $Name } | Select-Object -First 1
    if (-not $toolDef) { throw "Tool '$Name' not found in manifest" }
    $ver = $toolDef.version
    $exePath = Join-Path $ToolsDir "$Name/$ver/$Name$exeExt"
    if (-not (Test-Path $exePath)) { throw "Binary not found for '$Name' at: $exePath" }
    $hash = (Get-FileHash -Path $exePath -Algorithm SHA256).Hash.ToLowerInvariant()
    return @{
        Name = $Name
        Version = $ver
        Path = $exePath
        Sha256 = $hash
        License = $toolDef.license
    }
}

function Assert-NoMachinePath([string]$text, [string]$label) {
    if ($text -match '(?i)(C:\\Users|/Users/|/home/[a-zA-Z0-9_-]+|OneDrive)' -and $text -notmatch '<USER_PROFILE>|<USER_CACHE>|<CLOUD_HOME>') {
        throw "$label contains private machine path leak"
    }
}

$gitleaksReport = Join-Path $evidenceFullDir "gitleaks-report.json"
$trivyConfigReport = Join-Path $evidenceFullDir "trivy-config-report.json"
$trivyImageReport = Join-Path $evidenceFullDir "trivy-image-report.json"
$sbomReport = Join-Path $evidenceFullDir "sbom.cdx.json"
$committedSbom = Join-Path $repoRoot "artifacts\sbom.cdx.json"
$grypeReport = Join-Path $evidenceFullDir "grype-report.json"
$evidenceSummary = Join-Path $evidenceFullDir "security-summary.json"

$requiredFiles = @(
    @{ Path = $gitleaksReport; Name = "Gitleaks report"; Scanner = "gitleaks" },
    @{ Path = $trivyConfigReport; Name = "Trivy config report"; Scanner = "trivy-config" },
    @{ Path = $trivyImageReport; Name = "Trivy image report"; Scanner = "trivy-image" },
    @{ Path = $sbomReport; Name = "Run-owned CycloneDX SBOM"; Scanner = "syft" },
    @{ Path = $grypeReport; Name = "Grype report"; Scanner = "grype" }
)

foreach ($rf in $requiredFiles) {
    if (-not (Test-Path $rf.Path)) {
        throw "Security summary generation failed: $($rf.Name) missing from disk at '$($rf.Path)'"
    }
}

$gitleaksHash = Assert-RunSidecar -ArtifactPath $gitleaksReport -ExpectedScanner "gitleaks"
$trivyConfigHash = Assert-RunSidecar -ArtifactPath $trivyConfigReport -ExpectedScanner "trivy-config"
$trivyImageHash = Assert-RunSidecar -ArtifactPath $trivyImageReport -ExpectedScanner "trivy-image"
$sbomHash = Assert-RunSidecar -ArtifactPath $sbomReport -ExpectedScanner "syft"
$grypeHash = Assert-RunSidecar -ArtifactPath $grypeReport -ExpectedScanner "grype"

$sbomJson = Get-Content $sbomReport -Raw | ConvertFrom-Json
if ($sbomJson.bomFormat -ne "CycloneDX") { throw "SBOM bomFormat is not 'CycloneDX'" }
if (-not $sbomJson.components -or $sbomJson.components.Count -eq 0) { throw "SBOM components count is 0" }
if (-not $sbomJson.metadata -or -not $sbomJson.metadata.component -or $sbomJson.metadata.component.name -ne "dxos-api") {
    throw "SBOM metadata component does not identify deliverable 'dxos-api'"
}
Assert-NoMachinePath (Get-Content $sbomReport -Raw) "SBOM"

$sbomSeen = @{}
$sbomDuplicates = @()
foreach ($c in $sbomJson.components) {
    $purl = if ($c.purl) { $c.purl.ToLowerInvariant() } else { "" }
    $canonicalKey = if ($purl) { $purl } else { "$($c.type):$($c.name)@$($c.version)".ToLowerInvariant() }
    if ($sbomSeen.ContainsKey($canonicalKey)) {
        $sbomDuplicates += $canonicalKey
    } else {
        $sbomSeen[$canonicalKey] = $true
    }
}
if ($sbomDuplicates.Count -ne 0) {
    throw "SBOM duplicate validation failed: $($sbomDuplicates.Count) duplicate canonical component identities found ($($sbomDuplicates -join ', '))"
}

if (-not (Test-Path $committedSbom)) {
    throw "Committed SBOM missing at: $committedSbom"
}
$committedSbomHash = Get-FileSha256Upper $committedSbom
if ($committedSbomHash -ne $sbomHash) {
    throw "Committed SBOM hash '$committedSbomHash' does not match run-owned SBOM hash '$sbomHash'."
}

if ($ValidateExisting) {
    if (-not (Test-Path $CommittedSummaryPath)) {
        throw "ValidateExisting failed: summary missing at $CommittedSummaryPath"
    }
    $existing = Get-Content $CommittedSummaryPath -Raw | ConvertFrom-Json
    if ($existing.runId -ne $RunId) {
        throw "ValidateExisting failed: summary runId '$($existing.runId)' != required '$RunId'."
    }
    $recorded = @{
        gitleaks = $existing.reports.gitleaks.sha256.ToUpperInvariant()
        trivyConfig = $existing.reports.trivyConfig.sha256.ToUpperInvariant()
        trivyImage = $existing.reports.trivyImage.sha256.ToUpperInvariant()
        syftSbom = $existing.reports.syftSbom.sha256.ToUpperInvariant()
        grype = $existing.reports.grype.sha256.ToUpperInvariant()
    }
    $actual = @{
        gitleaks = $gitleaksHash
        trivyConfig = $trivyConfigHash
        trivyImage = $trivyImageHash
        syftSbom = $sbomHash
        grype = $grypeHash
    }
    foreach ($k in $actual.Keys) {
        if ($recorded[$k] -ne $actual[$k]) {
            throw "ValidateExisting failed: stale hash for '$k'. recorded='$($recorded[$k])' actual='$($actual[$k])'"
        }
    }
    Write-Host "PASS: Existing security summary hashes match current run-owned reports for RunId $RunId." -ForegroundColor Green
    return
}

$gitleaksTool = Get-StrictToolInfo "gitleaks"
$trivyTool = Get-StrictToolInfo "trivy"
$syftTool = Get-StrictToolInfo "syft"
$grypeTool = Get-StrictToolInfo "grype"

$glJson = Get-Content $gitleaksReport -Raw | ConvertFrom-Json
$glCount = if ($glJson) { @($glJson).Count } else { 0 }
if ($glCount -ne 0) { throw "Gitleaks report contains $glCount leak finding(s)" }

$tcJson = Get-Content $trivyConfigReport -Raw | ConvertFrom-Json
$tcMisconfigs = 0
if ($tcJson.Results) {
    foreach ($r in $tcJson.Results) {
        if ($r.Misconfigurations) { $tcMisconfigs += $r.Misconfigurations.Count }
    }
}
if ($tcMisconfigs -ne 0) { throw "Trivy config report contains $tcMisconfigs HIGH/CRITICAL misconfiguration(s)" }

$tiJson = Get-Content $trivyImageReport -Raw | ConvertFrom-Json
$tiVulns = 0
if ($tiJson.Results) {
    foreach ($r in $tiJson.Results) {
        if ($r.Vulnerabilities) { $tiVulns += $r.Vulnerabilities.Count }
    }
}
if ($tiVulns -ne 0) { throw "Trivy image report contains $tiVulns HIGH/CRITICAL vulnerability(ies)" }

$grJson = Get-Content $grypeReport -Raw | ConvertFrom-Json
$grMatches = if ($grJson.matches) { @($grJson.matches).Count } else { 0 }
if ($grMatches -ne 0) { throw "Grype report contains $grMatches vulnerability match(es) at/above threshold" }

$trivyVerOutput = & $trivyTool.Path version 2>&1 | Out-String
$trivyDbVersion = ""
$trivyDbUpdatedAt = ""
$trivyDbDownloadedAt = ""
$trivyCheckBundleDigest = ""
if ($trivyVerOutput -match "Vulnerability DB:\s*\r?\n\s*Version:\s*([^\r\n]+)") { $trivyDbVersion = $matches[1].Trim() }
if ($trivyVerOutput -match "UpdatedAt:\s*([^\r\n]+)") { $trivyDbUpdatedAt = $matches[1].Trim() }
if ($trivyVerOutput -match "DownloadedAt:\s*([^\r\n]+)") { $trivyDbDownloadedAt = $matches[1].Trim() }
if ($trivyVerOutput -match "Digest:\s*([^\r\n]+)") { $trivyCheckBundleDigest = $matches[1].Trim() }
if ([string]::IsNullOrWhiteSpace($trivyDbVersion) -or [string]::IsNullOrWhiteSpace($trivyDbUpdatedAt) -or [string]::IsNullOrWhiteSpace($trivyCheckBundleDigest)) {
    throw "Trivy database evidence incomplete. Raw output:`n$trivyVerOutput"
}

$grypeDbStatusOutput = & $grypeTool.Path db status 2>&1 | Out-String
$grypeDbSchema = ""
$grypeDbBuilt = ""
$grypeDbChecksum = ""
$grypeDbStatus = ""
if ($grypeDbStatusOutput -match "Schema:\s*([^\r\n]+)") { $grypeDbSchema = $matches[1].Trim() }
if ($grypeDbStatusOutput -match "Built:\s*([^\r\n]+)") { $grypeDbBuilt = $matches[1].Trim() }
if ($grypeDbStatusOutput -match "checksum=([^\r\n\s]+)") {
    $grypeDbChecksum = [System.Uri]::UnescapeDataString($matches[1].Trim())
}
if ($grypeDbStatusOutput -match "Status:\s*([^\r\n]+)") { $grypeDbStatus = $matches[1].Trim() }
if ([string]::IsNullOrWhiteSpace($grypeDbSchema) -or [string]::IsNullOrWhiteSpace($grypeDbBuilt) -or [string]::IsNullOrWhiteSpace($grypeDbChecksum) -or [string]::IsNullOrWhiteSpace($grypeDbStatus)) {
    throw "Grype database evidence incomplete. Raw output:`n$grypeDbStatusOutput"
}

$sanitizedTrivyRaw = Protect-MachinePaths ($trivyVerOutput.Trim())
$sanitizedGrypeRaw = Protect-MachinePaths ($grypeDbStatusOutput.Trim())
Assert-NoMachinePath $sanitizedTrivyRaw "Trivy DB raw output"
Assert-NoMachinePath $sanitizedGrypeRaw "Grype DB raw output"

$imageDigest = (docker inspect --format="{{index .Id}}" dxos-api:0.1.0-spike 2>$null)
if (-not $imageDigest) {
    throw "Deliverable image digest for dxos-api:0.1.0-spike is required and could not be inspected."
}
$imageDigest = $imageDigest.Trim()

$revisionSha = (git rev-parse HEAD).Trim()

$summary = [ordered]@{
    schemaVersion = "1.0"
    runId = $RunId
    revisionSha = $revisionSha
    generatedAt = (Get-Date).ToString("o")
    deliverable = [ordered]@{
        name = "dxos-api"
        version = "0.1.0-spike"
        imageName = "dxos-api:0.1.0-spike"
        imageDigest = $imageDigest
    }
    sbom = [ordered]@{
        path = "artifacts/sbom.cdx.json"
        sha256 = $sbomHash
        format = $sbomJson.bomFormat
        specVersion = $sbomJson.specVersion
        componentCount = $sbomJson.components.Count
        duplicateCount = 0
        canonicalKeyRule = "purl (case-normalized) if present, else type:name@version (case-normalized)"
    }
    tools = [ordered]@{
        gitleaks = [ordered]@{ version = $gitleaksTool.Version; sha256 = $gitleaksTool.Sha256; license = $gitleaksTool.License }
        trivy = [ordered]@{ version = $trivyTool.Version; sha256 = $trivyTool.Sha256; license = $trivyTool.License }
        syft = [ordered]@{ version = $syftTool.Version; sha256 = $syftTool.Sha256; license = $syftTool.License }
        grype = [ordered]@{ version = $grypeTool.Version; sha256 = $grypeTool.Sha256; license = $grypeTool.License }
    }
    scannerDatabases = [ordered]@{
        trivy = [ordered]@{
            vulnerabilityDbVersion = $trivyDbVersion
            updatedAt = $trivyDbUpdatedAt
            downloadedAt = $trivyDbDownloadedAt
            checkBundleDigest = $trivyCheckBundleDigest
            rawOutput = $sanitizedTrivyRaw
        }
        grype = [ordered]@{
            schema = $grypeDbSchema
            built = $grypeDbBuilt
            checksum = $grypeDbChecksum
            status = $grypeDbStatus
            rawOutput = $sanitizedGrypeRaw
        }
    }
    reports = [ordered]@{
        gitleaks = [ordered]@{ path = "gitleaks-report.json"; sha256 = $gitleaksHash; findings = $glCount }
        trivyConfig = [ordered]@{ path = "trivy-config-report.json"; sha256 = $trivyConfigHash; misconfigurations = $tcMisconfigs }
        trivyImage = [ordered]@{ path = "trivy-image-report.json"; sha256 = $trivyImageHash; vulnerabilities = $tiVulns }
        syftSbom = [ordered]@{ path = "artifacts/sbom.cdx.json"; sha256 = $sbomHash; components = $sbomJson.components.Count }
        grype = [ordered]@{ path = "grype-report.json"; sha256 = $grypeHash; vulnerabilities = $grMatches }
    }
    evidenceDir = $evidenceFullDir.Substring($repoRoot.Length).TrimStart('\', '/').Replace('\', '/')
    policies = [ordered]@{
        gitleaks = "zero-leaks"
        trivy = "zero-high-critical"
        grype = "fail-on-high"
    }
    dispositions = [ordered]@{
        gitleaks = "PASS"
        trivyConfig = "PASS"
        trivyImage = "PASS"
        syftSbom = "PASS"
        grype = "PASS"
    }
    overallVerdict = "PASS"
}

$summaryJson = Protect-MachinePaths ($summary | ConvertTo-Json -Depth 15)
Assert-NoMachinePath $summaryJson "Security summary"

$canonicalFull = [System.IO.Path]::GetFullPath($CommittedSummaryPath)
$evidenceFull = [System.IO.Path]::GetFullPath($evidenceSummary)
$canonicalBackup = $null

try {
    if (Test-Path $canonicalFull) {
        $canonicalBackup = [System.IO.File]::ReadAllText($canonicalFull)
    }
    Set-AtomicFileContent -Destination $canonicalFull -Content $summaryJson

    if ($evidenceFull -ne $canonicalFull) {
        try {
            Set-AtomicFileContent -Destination $evidenceFull -Content $summaryJson
        } catch {
            if ($null -ne $canonicalBackup) {
                Set-AtomicFileContent -Destination $canonicalFull -Content $canonicalBackup
            } elseif (Test-Path $canonicalFull) {
                Remove-Item -Path $canonicalFull -Force
            }
            throw
        }
    }

    $canonicalHash = Get-FileSha256Upper $canonicalFull
    $evidenceHash = Get-FileSha256Upper $evidenceFull
    if ($canonicalHash -ne $evidenceHash) {
        throw "Atomic summary destinations diverged: canonical '$canonicalHash' vs evidence '$evidenceHash'."
    }

    $readBack = Get-Content $canonicalFull -Raw | ConvertFrom-Json
    if ($readBack.overallVerdict -ne "PASS") { throw "Validation failed: overallVerdict is not PASS" }
    if ($readBack.runId -ne $RunId) { throw "Validation failed: runId mismatch in summary" }
    if ($readBack.reports.gitleaks.sha256 -ne $gitleaksHash) { throw "Validation failed: Gitleaks report hash mismatch" }
    if ($readBack.reports.trivyConfig.sha256 -ne $trivyConfigHash) { throw "Validation failed: Trivy config hash mismatch" }
    if ($readBack.reports.trivyImage.sha256 -ne $trivyImageHash) { throw "Validation failed: Trivy image hash mismatch" }
    if ($readBack.reports.syftSbom.sha256 -ne $sbomHash) { throw "Validation failed: SBOM hash mismatch" }
    if ($readBack.reports.grype.sha256 -ne $grypeHash) { throw "Validation failed: Grype report hash mismatch" }

    Write-Host "Atomically generated and verified security summary (RunId: $RunId) at $canonicalFull" -ForegroundColor Green
} catch {
    throw
}
