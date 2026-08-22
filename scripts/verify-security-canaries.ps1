[CmdletBinding()]
param(
    [string]$ToolsDir = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item $PSScriptRoot\..).FullName

if (-not $ToolsDir) {
    $ToolsDir = Join-Path $repoRoot ".tools\security"
}
$ToolsDir = [System.IO.Path]::GetFullPath($ToolsDir)

$manifestPath = Join-Path $PSScriptRoot "security-tools.json"
if (-not (Test-Path $manifestPath)) {
    throw "Security tools manifest not found at: $manifestPath"
}
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

$platform = "windows-x64"
$exeExt = ".exe"
if ($IsLinux) {
    $platform = "linux-x64"
    $exeExt = ""
}

function Assert-SafePathChain {
    param([string]$Path, [string]$ExpectedRoot)
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $rootFullPath = [System.IO.Path]::GetFullPath($ExpectedRoot)

    if (-not (Test-Path $rootFullPath)) {
        throw "Security violation: Root '$rootFullPath' does not exist"
    }
    $rootItem = Get-Item $rootFullPath -Force
    if ($rootItem.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
        throw "Security violation: Root '$rootFullPath' is a reparse point"
    }

    $rootWithSep = $rootFullPath
    if (-not $rootWithSep.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $rootWithSep += [System.IO.Path]::DirectorySeparatorChar
    }
    if (-not $fullPath.StartsWith($rootWithSep, [System.StringComparison]::OrdinalIgnoreCase) -and -not $fullPath.Equals($rootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Security violation: Path '$fullPath' escapes expected root '$rootFullPath'"
    }

    $current = $fullPath
    while ($true) {
        if (Test-Path $current) {
            $item = Get-Item $current -Force
            if ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
                throw "Security violation: Path '$current' in ancestor chain is a reparse point"
            }
        }
        if ($current.Equals($rootFullPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }
        $parentDir = [System.IO.Path]::GetDirectoryName($current)
        if ([string]::IsNullOrEmpty($parentDir) -or $parentDir -eq $current) {
            break
        }
        $current = $parentDir
    }
    return $fullPath
}

function Find-StrictTool([string]$Name) {
    $toolDef = $manifest.tools | Where-Object { $_.name -eq $Name } | Select-Object -First 1
    if (-not $toolDef) {
        throw "Tool '$Name' not defined in security manifest."
    }
    $ver = $toolDef.version
    $artifact = $toolDef.artifacts.$platform
    if (-not $artifact) {
        throw "Platform '$platform' not supported for '$Name'."
    }

    $localExe = Join-Path $ToolsDir "$Name/$ver/$Name$exeExt"
    if (-not (Test-Path $localExe)) {
        throw "Required tool '$Name' ($ver) not found at: $localExe."
    }

    [void](Assert-SafePathChain $localExe $ToolsDir)

    $actualHash = (Get-FileHash -Path $localExe -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($artifact.executableSha256 -and ($actualHash -ne $artifact.executableSha256.ToLowerInvariant())) {
        throw "Integrity violation: '$Name' executable hash mismatch!"
    }

    return @{
        Name = $Name
        Version = $ver
        Path = $localExe
        Sha256 = $actualHash
    }
}

$gitleaks = Find-StrictTool "gitleaks"
$trivy = Find-StrictTool "trivy"
$syft = Find-StrictTool "syft"
$grype = Find-StrictTool "grype"

$scratchDir = Join-Path $ToolsDir ".staging\canaries-$([guid]::NewGuid().ToString('N'))"
[void](New-Item -ItemType Directory -Path $scratchDir -Force)

try {
    Write-Host "=== Starting Security Scanner Canary Verifications ===" -ForegroundColor Cyan

    # -------------------------------------------------------------
    # 1. Gitleaks Secret Detection Canary
    # -------------------------------------------------------------
    Write-Host "`n1. Testing Gitleaks secret canary..." -ForegroundColor Cyan
    $gitleaksDir = Join-Path $scratchDir "gitleaks-fixture"
    [void](New-Item -ItemType Directory -Path $gitleaksDir -Force)
    $secretFile = Join-Path $gitleaksDir "fixture_secret.env"
    $syntheticToken = "ghp_" + "1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f"
    [System.IO.File]::WriteAllText($secretFile, "GITHUB_TOKEN='$syntheticToken'", [System.Text.UTF8Encoding]::new($false))
    $gitleaksReport = Join-Path $scratchDir "gitleaks_canary_report.json"

    $proc = Start-Process -FilePath $gitleaks.Path -ArgumentList "detect", "--source", "`"$gitleaksDir`"", "--no-git", "--report-format", "json", "--report-path", "`"$gitleaksReport`"", "--exit-code", "1" -NoNewWindow -PassThru
    [void]($proc | Wait-Process -Timeout 30 -ErrorAction Stop)
    $glExit = $proc.ExitCode

    if ($glExit -eq 0) {
        throw "Gitleaks canary failed: scanner did not detect synthetic secret (exit code 0)."
    }
    if (-not (Test-Path $gitleaksReport)) {
        throw "Gitleaks canary failed: report file was not generated."
    }
    $glFindings = Get-Content $gitleaksReport -Raw | ConvertFrom-Json
    if (-not $glFindings -or $glFindings.Count -eq 0) {
        throw "Gitleaks canary failed: report contains 0 findings."
    }
    $expectedRule = "github-pat"
    if ($glFindings[0].RuleID -ne $expectedRule) {
        throw "Gitleaks canary failed: expected RuleID '$expectedRule', got '$($glFindings[0].RuleID)'"
    }
    Write-Host "PASS: Gitleaks detected synthetic secret ($($glFindings.Count) finding(s), RuleID: $($glFindings[0].RuleID), exit code: $glExit)" -ForegroundColor Green

    # -------------------------------------------------------------
    # 2. Trivy Config/Misconfiguration Canary
    # -------------------------------------------------------------
    Write-Host "`n2. Testing Trivy misconfiguration canary..." -ForegroundColor Cyan
    $trivyDir = Join-Path $scratchDir "trivy-fixture"
    [void](New-Item -ItemType Directory -Path $trivyDir -Force)
    $badDockerfile = Join-Path $trivyDir "Dockerfile"
    $badDockerContent = "FROM alpine:3.19`nADD http://example.com/malicious.tar.gz /tmp/`nUSER root`nCMD [`"sh`"]`n"
    [System.IO.File]::WriteAllText($badDockerfile, $badDockerContent, [System.Text.UTF8Encoding]::new($false))

    $trivyReport = Join-Path $scratchDir "trivy_canary_report.json"
    $proc = Start-Process -FilePath $trivy.Path -ArgumentList "config", "`"$trivyDir`"", "--format", "json", "--output", "`"$trivyReport`"", "--severity", "HIGH,CRITICAL", "--exit-code", "1", "--skip-version-check" -NoNewWindow -PassThru
    [void]($proc | Wait-Process -Timeout 60 -ErrorAction Stop)
    $trivyExit = $proc.ExitCode

    if (-not (Test-Path $trivyReport)) {
        throw "Trivy canary failed: report file was not generated."
    }
    $trivyJson = Get-Content $trivyReport -Raw | ConvertFrom-Json
    $misconfIds = @()
    if ($trivyJson.Results) {
        foreach ($r in $trivyJson.Results) {
            if ($r.Misconfigurations) {
                foreach ($m in $r.Misconfigurations) {
                    $misconfIds += $m.ID
                }
            }
        }
    }
    if ($misconfIds.Count -eq 0 -or $trivyExit -eq 0) {
        throw "Trivy canary failed: misconfigurations were not detected on fixture (exit: $trivyExit)."
    }
    Write-Host "PASS: Trivy detected misconfigurations ($($misconfIds.Count) finding(s): $($misconfIds -join ', '), exit code: $trivyExit)" -ForegroundColor Green

    # -------------------------------------------------------------
    # 3. Syft CycloneDX SBOM Generation Canary
    # -------------------------------------------------------------
    Write-Host "`n3. Testing Syft SBOM generation canary..." -ForegroundColor Cyan
    $syftDir = Join-Path $scratchDir "syft-fixture"
    [void](New-Item -ItemType Directory -Path $syftDir -Force)
    [System.IO.File]::WriteAllText((Join-Path $syftDir "requirements.txt"), "requests==2.31.0`nurllib3==2.0.7`n", [System.Text.UTF8Encoding]::new($false))
    $syftReport = Join-Path $scratchDir "syft_canary_sbom.cdx.json"

    $proc = Start-Process -FilePath $syft.Path -ArgumentList "dir:`"$syftDir`"", "-o", "cyclonedx-json=`"$syftReport`"" -NoNewWindow -PassThru
    [void]($proc | Wait-Process -Timeout 30 -ErrorAction Stop)
    if ($proc.ExitCode -ne 0) {
        throw "Syft canary failed with exit code $($proc.ExitCode)"
    }
    if (-not (Test-Path $syftReport)) {
        throw "Syft canary failed: SBOM file was not generated."
    }
    $syftJson = Get-Content $syftReport -Raw | ConvertFrom-Json
    $compNamesList = @($syftJson.components | ForEach-Object { $_.name })
    if ($syftJson.bomFormat -ne "CycloneDX" -or -not $syftJson.components -or $syftJson.components.Count -lt 2 -or -not ($compNamesList -contains "requests") -or -not ($compNamesList -contains "urllib3")) {
        throw "Syft canary failed: invalid CycloneDX output or component mismatch (expected requests and urllib3, got $($compNamesList -join ', '))."
    }
    $compNames = $compNamesList -join ', '
    Write-Host "PASS: Syft generated valid CycloneDX SBOM ($($syftJson.components.Count) component(s): $compNames, specVersion: $($syftJson.specVersion))" -ForegroundColor Green

    # -------------------------------------------------------------
    # 4. Grype Vulnerability Detection & Policy Rejection Canary
    # -------------------------------------------------------------
    Write-Host "`n4. Testing Grype vulnerability detection and policy rejection canary..." -ForegroundColor Cyan
    $vulnSbom = Join-Path $scratchDir "grype_vuln_canary.cdx.json"
    $vulnSbomContent = @{
        bomFormat = "CycloneDX"
        specVersion = "1.5"
        version = 1
        components = @(
            @{
                name = "log4j-core"
                version = "2.14.1"
                type = "library"
                purl = "pkg:maven/org.apache.logging.log4j/log4j-core@2.14.1"
            }
        )
    } | ConvertTo-Json -Depth 5
    [System.IO.File]::WriteAllText($vulnSbom, $vulnSbomContent, [System.Text.UTF8Encoding]::new($false))
    $grypeReport = Join-Path $scratchDir "grype_canary_report.json"

    $arg1 = "sbom:`"$vulnSbom`""
    $proc = Start-Process -FilePath $grype.Path -ArgumentList $arg1, "--fail-on", "high", "--output", "json" -NoNewWindow -PassThru -RedirectStandardOutput $grypeReport
    [void]($proc | Wait-Process -Timeout 60 -ErrorAction Stop)
    $grypeExit = $proc.ExitCode

    if ($grypeExit -eq 0) {
        throw "Grype canary failed: scanner did not fail on high/critical vulnerability in controlled fixture (exit code 0)."
    }
    if (-not (Test-Path $grypeReport)) {
        throw "Grype canary failed: output report was not generated."
    }
    $grypeJson = Get-Content $grypeReport -Raw | ConvertFrom-Json
    if (-not $grypeJson.matches -or $grypeJson.matches.Count -eq 0) {
        throw "Grype canary failed: 0 matches returned for known vulnerable fixture."
    }
    $vulnIds = @()
    foreach ($m in $grypeJson.matches) {
        if ($m.vulnerability.id) { $vulnIds += $m.vulnerability.id }
        if ($m.relatedVulnerabilities) {
            foreach ($rv in $m.relatedVulnerabilities) {
                if ($rv.id) { $vulnIds += $rv.id }
            }
        }
    }
    $vulnIds = $vulnIds | Select-Object -Unique
    if (-not ($vulnIds -match "CVE-2021-44228|CVE-2021-45046|GHSA-jfh8-c2jp-5v3q|GHSA-7rjr-3q55-vv33|GHSA-j2ge-4hvm-797r")) {
        throw "Grype canary failed: expected log4j CVE/GHSA not found in matches ($($vulnIds -join ', '))"
    }
    Write-Host "PASS: Grype rejected vulnerable fixture ($($grypeJson.matches.Count) match(es): $($vulnIds -join ', '), exit code: $grypeExit)" -ForegroundColor Green

    Write-Host "`nAll scanner canary tests PASSED." -ForegroundColor Green
} finally {
    if (Test-Path $scratchDir) {
        Remove-Item -Path $scratchDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
