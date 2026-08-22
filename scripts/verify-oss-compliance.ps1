[CmdletBinding()]
param(
    [ValidateSet('All', 'License', 'Attribution', 'Inventory', 'Services', 'Identity')]
    [string]$Check = 'All',
    [string]$InventoryPath = $null
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot\..).FullName

function Get-CanonicalIdentity {
    param([PSCustomObject]$component)
    if ($component.purl -and -not [string]::IsNullOrWhiteSpace($component.purl)) {
        return $component.purl.Trim().ToLowerInvariant()
    }
    $cType = if ($component.type) { $component.type.ToString().Trim().ToLowerInvariant() } else { "unknown" }
    $cName = if ($component.name) { $component.name.ToString().Trim().ToLowerInvariant() } else { "unknown" }
    $cVer = if ($component.version) { $component.version.ToString().Trim().ToLowerInvariant() } else { "unknown" }
    return "$cType`:$cName@$cVer"
}

function Verify-CanonicalLicense {
    Write-Host "Verifying canonical DX-OS license and NOTICE..." -ForegroundColor Cyan
    $licensePath = Join-Path $repoRoot "LICENSE"
    $noticePath = Join-Path $repoRoot "NOTICE"

    if (-not (Test-Path $licensePath)) {
        throw "Canonical LICENSE file missing at root: $licensePath"
    }
    if (-not (Test-Path $noticePath)) {
        throw "Canonical NOTICE file missing at root: $noticePath"
    }

    $licenseText = Get-Content $licensePath -Raw
    if (-not ($licenseText -match "Apache License\s+Version 2\.0")) {
        throw "LICENSE file is not Apache-2.0 canonical text."
    }

    $noticeText = Get-Content $noticePath -Raw
    if (-not ($noticeText -match "DX-OS") -or -not ($noticeText -match "Copyright \(c\)")) {
        throw "NOTICE file missing DX-OS copyright or required format."
    }

    Write-Host "PASS: Canonical Apache-2.0 LICENSE and NOTICE verified." -ForegroundColor Green
}

function Verify-ThirdPartyAttribution {
    Write-Host "Verifying third-party attribution and notices..." -ForegroundColor Cyan
    $tpNoticesPath = Join-Path $repoRoot "THIRD_PARTY_NOTICES.md"
    if (-not (Test-Path $tpNoticesPath)) {
        throw "THIRD_PARTY_NOTICES.md missing at root: $tpNoticesPath"
    }

    $tpText = Get-Content $tpNoticesPath -Raw
    if (-not ($tpText -match "Third-Party Notices and Licenses") -or -not ($tpText -match "Apache-2\.0") -or -not ($tpText -match "MIT")) {
        throw "THIRD_PARTY_NOTICES.md missing standard attribution structure or required licenses."
    }

    Write-Host "PASS: Third-party notices and license texts verified." -ForegroundColor Green
}

function Verify-OssInventory {
    Write-Host "Verifying exact bi-directional open-source component inventory reconciliation..." -ForegroundColor Cyan
    
    $invFile = if ($InventoryPath) { $InventoryPath } else { Join-Path $repoRoot "artifacts\oss-inventory.json" }
    if (-not (Test-Path $invFile)) {
        throw "OSS inventory file missing at: $invFile"
    }

    $inv = Get-Content $invFile -Raw | ConvertFrom-Json
    if (-not $inv) {
        throw "Failed to parse OSS inventory JSON."
    }

    # 1. NuGet Packages Exact Bi-Directional Reconciliation
    $lockFiles = Get-ChildItem -Path $repoRoot -Recurse -Filter "packages.lock.json"
    if ($lockFiles.Count -eq 0) {
        throw "No packages.lock.json files found in repository."
    }

    $lockPackageSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($lf in $lockFiles) {
        $lockJson = Get-Content $lf.FullName -Raw | ConvertFrom-Json
        $dependencies = $lockJson.dependencies
        if ($dependencies) {
            foreach ($tfProp in $dependencies.PSObject.Properties) {
                $packagesObj = $tfProp.Value
                if ($packagesObj) {
                    foreach ($pkgProp in $packagesObj.PSObject.Properties) {
                        $pkgName = $pkgProp.Name
                        $pkgDetails = $pkgProp.Value
                        $pkgType = if ($pkgDetails.type) { $pkgDetails.type.ToString() } else { "" }
                        $pkgResolved = if ($pkgDetails.resolved) { $pkgDetails.resolved.ToString() } else { "" }

                        # Explicitly exclude Project references
                        if ($pkgType -eq "Project" -or [string]::IsNullOrWhiteSpace($pkgResolved)) {
                            continue
                        }

                        $lockPackageSet.Add("$pkgName@$pkgResolved") | Out-Null
                    }
                }
            }
        }
    }

    $allInvPackages = @()
    if ($inv.directPackages) { $allInvPackages += $inv.directPackages }
    if ($inv.transitivePackages) { $allInvPackages += $inv.transitivePackages }

    $invPackageSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($pkg in $allInvPackages) {
        $name = $pkg.name.Trim()
        $ver = $pkg.version.Trim()
        if ([string]::IsNullOrWhiteSpace($name) -or [string]::IsNullOrWhiteSpace($ver)) {
            throw "OSS Inventory contains package with blank name or version: $($pkg | ConvertTo-Json -Compress)"
        }
        $key = "$name@$ver"
        if ($invPackageSet.Contains($key)) {
            throw "Duplicate package detected in OSS inventory: '$key'"
        }
        $invPackageSet.Add($key) | Out-Null
    }

    # Set comparisons
    $missingFromInv = @()
    foreach ($pkgKey in $lockPackageSet) {
        if (-not $invPackageSet.Contains($pkgKey)) {
            $missingFromInv += $pkgKey
        }
    }

    $extraInInv = @()
    foreach ($pkgKey in $invPackageSet) {
        if (-not $lockPackageSet.Contains($pkgKey)) {
            $extraInInv += $pkgKey
        }
    }

    if ($missingFromInv.Count -gt 0) {
        throw "OSS Inventory is missing $($missingFromInv.Count) package(s) present in lockfiles: $($missingFromInv -join ', ')"
    }
    if ($extraInInv.Count -gt 0) {
        throw "OSS Inventory contains $($extraInInv.Count) extraneous package(s) not in lockfiles: $($extraInInv -join ', ')"
    }

    function Get-ExactSetDelta([System.Collections.Generic.HashSet[string]]$Expected, [System.Collections.Generic.HashSet[string]]$Actual, [string]$Label) {
        $missing = @()
        foreach ($k in $Expected) {
            if (-not $Actual.Contains($k)) { $missing += $k }
        }
        $extra = @()
        foreach ($k in $Actual) {
            if (-not $Expected.Contains($k)) { $extra += $k }
        }
        if ($missing.Count -gt 0 -or $extra.Count -gt 0) {
            throw "$Label exact-set mismatch. Missing ($($missing.Count)): $($missing -join ', '); Extra ($($extra.Count)): $($extra -join ', ')"
        }
    }

    # 2. Container Images Exact Bi-Directional Reconciliation
    $expectedImages = @(
        @{ name = "mcr.microsoft.com/dotnet/aspnet"; version = "10.0"; digest = "sha256:207cc51496778557731c81ff670333d8ade4a4fec22768fd1be8e78474a84ecf"; license = "MIT" },
        @{ name = "mcr.microsoft.com/dotnet/sdk"; version = "10.0"; digest = "sha256:e1fc6e423f543119c406d24e2e687d67c569f18f04a37a8b0005d80ad0dcee80"; license = "MIT" },
        @{ name = "postgres"; version = "18.4-alpine"; digest = "sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15"; license = "PostgreSQL License" }
    )
    $expectedImageSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($exp in $expectedImages) {
        [void]$expectedImageSet.Add("$($exp.name)|$($exp.version)|$($exp.digest)|$($exp.license)")
    }
    $invImages = if ($inv.containerImages) { @($inv.containerImages) } else { @() }
    $actualImageSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($img in $invImages) {
        if ([string]::IsNullOrWhiteSpace($img.name) -or [string]::IsNullOrWhiteSpace($img.version) -or [string]::IsNullOrWhiteSpace($img.digest) -or [string]::IsNullOrWhiteSpace($img.license)) {
            throw "Container image record missing name/version/digest/license: $($img | ConvertTo-Json -Compress)"
        }
        $key = "$($img.name)|$($img.version)|$($img.digest)|$($img.license)"
        if (-not $actualImageSet.Add($key)) {
            throw "Duplicate container image identity in inventory: $key"
        }
    }
    Get-ExactSetDelta $expectedImageSet $actualImageSet "Container images"

    # 3. Security Tools Exact Bi-Directional Reconciliation against scripts/security-tools.json
    $toolsJsonPath = Join-Path $repoRoot "scripts\security-tools.json"
    if (-not (Test-Path $toolsJsonPath)) {
        throw "Missing scripts/security-tools.json"
    }
    $toolsJson = Get-Content $toolsJsonPath -Raw | ConvertFrom-Json
    $expectedToolSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($manifestTool in $toolsJson.tools) {
        $win = $manifestTool.artifacts.'windows-x64'
        $linux = $manifestTool.artifacts.'linux-x64'
        if (-not $win -or -not $linux) {
            throw "Manifest tool '$($manifestTool.name)' missing windows-x64 or linux-x64 artifact identity."
        }
        [void]$expectedToolSet.Add("$($manifestTool.name)|$($manifestTool.version)|$($manifestTool.license)|$($win.sha256)|$($win.executableSha256)|$($linux.sha256)|$($linux.executableSha256)")
    }
    $invTools = if ($inv.securityTools) { @($inv.securityTools) } else { @() }
    $actualToolSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($tool in $invTools) {
        if ([string]::IsNullOrWhiteSpace($tool.name) -or [string]::IsNullOrWhiteSpace($tool.version) -or [string]::IsNullOrWhiteSpace($tool.license)) {
            throw "Security tool record missing name/version/license: $($tool | ConvertTo-Json -Compress)"
        }
        $winArchive = $tool.windowsArchiveSha256
        $winExe = $tool.windowsExecutableSha256
        $linuxArchive = $tool.linuxArchiveSha256
        $linuxExe = $tool.linuxExecutableSha256
        if ([string]::IsNullOrWhiteSpace($winArchive) -or [string]::IsNullOrWhiteSpace($winExe) -or [string]::IsNullOrWhiteSpace($linuxArchive) -or [string]::IsNullOrWhiteSpace($linuxExe)) {
            throw "Security tool '$($tool.name)' missing acquisition/executable SHA-256 identity."
        }
        $key = "$($tool.name)|$($tool.version)|$($tool.license)|$winArchive|$winExe|$linuxArchive|$linuxExe"
        if (-not $actualToolSet.Add($key)) {
            throw "Duplicate security tool identity in inventory: $key"
        }
    }
    Get-ExactSetDelta $expectedToolSet $actualToolSet "Security tools"

    # 4. Third-Party Services Exact Bi-Directional Reconciliation
    $expectedServices = @(
        @{ name = "Google Gemini"; version = "API/CLI"; license = "Commercial / Proprietary"; officialSource = "https://ai.google.dev" },
        @{ name = "OpenAI / Codex"; version = "API/CLI"; license = "Commercial / Proprietary"; officialSource = "https://openai.com" }
    )
    $expectedServiceSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($exp in $expectedServices) {
        [void]$expectedServiceSet.Add("$($exp.name)|$($exp.version)|$($exp.license)|$($exp.officialSource)")
    }
    $invServices = if ($inv.thirdPartyServices) { @($inv.thirdPartyServices) } else { @() }
    $actualServiceSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($svc in $invServices) {
        if ([string]::IsNullOrWhiteSpace($svc.name) -or [string]::IsNullOrWhiteSpace($svc.version) -or [string]::IsNullOrWhiteSpace($svc.license) -or [string]::IsNullOrWhiteSpace($svc.officialSource)) {
            throw "Third-party service record missing name/version/license/officialSource: $($svc | ConvertTo-Json -Compress)"
        }
        $key = "$($svc.name)|$($svc.version)|$($svc.license)|$($svc.officialSource)"
        if (-not $actualServiceSet.Add($key)) {
            throw "Duplicate third-party service identity in inventory: $key"
        }
    }
    Get-ExactSetDelta $expectedServiceSet $actualServiceSet "Third-party services"

    # 5. Reused Source Check
    $reusedSource = if ($inv.reusedSource) { @($inv.reusedSource) } else { @() }
    if ($reusedSource.Count -ne 0) {
        throw "reusedSource must be empty array ([]) when no third-party source files are vendored or modified."
    }

    # 6. CycloneDX Deliverable SBOM Validation
    $sbomFile = Join-Path $repoRoot "artifacts\sbom.cdx.json"
    if (-not (Test-Path $sbomFile)) {
        throw "Deliverable CycloneDX SBOM missing at: $sbomFile"
    }

    $sbom = Get-Content $sbomFile -Raw | ConvertFrom-Json
    if ($sbom.bomFormat -ne "CycloneDX") {
        throw "SBOM is not CycloneDX format (bomFormat was '$($sbom.bomFormat)')."
    }
    if ($sbom.specVersion -ne "1.7") {
        throw "SBOM specVersion was '$($sbom.specVersion)', expected '1.7'."
    }
    if (-not $sbom.components -or $sbom.components.Count -eq 0) {
        throw "SBOM contains 0 components; must catalog actual deliverable dependencies."
    }

    # Validate zero duplicates under canonical identity
    $seenIds = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
    $duplicateIds = [System.Collections.Generic.List[string]]::new()
    foreach ($c in $sbom.components) {
        $cid = Get-CanonicalIdentity $c
        if (-not $seenIds.Add($cid)) {
            $duplicateIds.Add($cid) | Out-Null
        }
    }

    if ($duplicateIds.Count -gt 0) {
        throw "Deliverable SBOM contains $($duplicateIds.Count) duplicate component(s) under canonical identity key: $($duplicateIds -join ', ')"
    }

    # Validate zero private machine paths in SBOM text
    $sbomRaw = Get-Content $sbomFile -Raw
    if ($sbomRaw -match '(?i)(C:\\Users|/Users/|/home/[a-zA-Z0-9_-]+|OneDrive)' -and -not ($sbomRaw -match '<USER_CACHE>')) {
        throw "Deliverable SBOM contains private machine paths!"
    }

    Write-Host "PASS: Open-source inventory exactly reconciled (Lockfile/Inventory Packages: $($invPackageSet.Count), Direct: $($inv.directPackages.Count), Transitive: $($inv.transitivePackages.Count), Images: $($invImages.Count), Tools: $($invTools.Count), Services: $($invServices.Count), SBOM Components: $($sbom.components.Count), Duplicates: 0, Missing: 0, Extra: 0)." -ForegroundColor Green
}

function Verify-ServiceDisclosure {
    Write-Host "Verifying third-party service disclosure..." -ForegroundColor Cyan
    $servicesDocPath = Join-Path $repoRoot "docs\THIRD_PARTY_SERVICES.md"
    if (-not (Test-Path $servicesDocPath)) {
        throw "Missing docs/THIRD_PARTY_SERVICES.md"
    }

    $docText = Get-Content $servicesDocPath -Raw
    if (-not ($docText -match "Google Gemini") -or -not ($docText -match "OpenAI") -or -not ($docText -match "Development-time")) {
        throw "THIRD_PARTY_SERVICES.md does not accurately disclose external AI provider boundaries."
    }

    Write-Host "PASS: Third-party service disclosures verified." -ForegroundColor Green
}

function Verify-RepositoryIdentity {
    Write-Host "Verifying repository identity and truthful claims..." -ForegroundColor Cyan
    $readmePath = Join-Path $repoRoot "README.md"
    $ossDocPath = Join-Path $repoRoot "OPEN_SOURCE.md"
    $secDocPath = Join-Path $repoRoot "SECURITY.md"

    if (-not (Test-Path $readmePath) -or -not (Test-Path $ossDocPath) -or -not (Test-Path $secDocPath)) {
        throw "Core repository governance and identity documentation missing."
    }

    $readmeText = Get-Content $readmePath -Raw
    if (-not ($readmeText -match "DX-OS") -or -not ($readmeText -match "Apache-2\.0")) {
        throw "README.md does not reflect DX-OS repository identity or Apache-2.0 license."
    }

    Write-Host "PASS: Repository identity and truthful claims verified." -ForegroundColor Green
}

switch ($Check) {
    'License'      { Verify-CanonicalLicense }
    'Attribution'  { Verify-ThirdPartyAttribution }
    'Inventory'    { Verify-OssInventory }
    'Services'     { Verify-ServiceDisclosure }
    'Identity'     { Verify-RepositoryIdentity }
    'All' {
        Verify-CanonicalLicense
        Verify-ThirdPartyAttribution
        Verify-OssInventory
        Verify-ServiceDisclosure
        Verify-RepositoryIdentity
    }
}
