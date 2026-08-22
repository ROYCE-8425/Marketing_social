[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("All", "Trivy", "Gitleaks", "Syft", "Grype", "Summary")]
    [string]$Scanner = "All",

    [string]$EvidenceDir = "",
    [string]$ToolsDir = "",
    [string]$RunId = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = (Get-Item $PSScriptRoot\..).FullName

if (-not $ToolsDir) {
    $ToolsDir = Join-Path $repoRoot ".tools\security"
}
$ToolsDir = [System.IO.Path]::GetFullPath($ToolsDir)

if ([string]::IsNullOrWhiteSpace($RunId)) {
    $RunId = [guid]::NewGuid().ToString()
}

if (-not $EvidenceDir) {
    $evidenceFullDir = Join-Path $repoRoot "artifacts\quality-gate\run-$RunId"
} elseif ([System.IO.Path]::IsPathRooted($EvidenceDir)) {
    $evidenceFullDir = [System.IO.Path]::GetFullPath($EvidenceDir)
} else {
    $evidenceFullDir = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $EvidenceDir))
}

if (-not (Test-Path $evidenceFullDir)) {
    [void](New-Item -ItemType Directory -Path $evidenceFullDir -Force)
}

$securityDir = Join-Path $repoRoot "artifacts\security"
if (-not (Test-Path $securityDir)) {
    [void](New-Item -ItemType Directory -Path $securityDir -Force)
}

$manifestPath = Join-Path $PSScriptRoot "security-tools.json"
if (-not (Test-Path $manifestPath)) {
    throw "Manifest not found: $manifestPath"
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
        throw "Required tool '$Name' ($ver) not found at: $localExe. Run scripts/setup-security-tools.ps1."
    }

    [void](Assert-SafePathChain $localExe $ToolsDir)

    if ($toolDef.forbiddenVersions -and ($toolDef.forbiddenVersions -contains $ver)) {
        throw "Tool '$Name' version '$ver' is forbidden by security policy."
    }

    $actualHash = (Get-FileHash -Path $localExe -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($artifact.executableSha256 -and ($actualHash -ne $artifact.executableSha256.ToLowerInvariant())) {
        throw "Integrity violation: '$Name' executable hash mismatch! (Expected: $($artifact.executableSha256), Found: $actualHash)"
    }

    return @{
        Name = $Name
        Version = $ver
        Path = $localExe
        Sha256 = $actualHash
        License = $toolDef.license
    }
}

function Write-RunSidecar {
    param(
        [string]$ArtifactPath,
        [string]$ScannerName
    )
    if ([string]::IsNullOrWhiteSpace($RunId)) {
        throw "RunId is required to bind scanner output '$ArtifactPath'."
    }
    if (-not (Test-Path $ArtifactPath)) {
        throw "Cannot write sidecar; artifact missing: $ArtifactPath"
    }
    $hash = (Get-FileHash -Path $ArtifactPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $meta = @{
        runId = $RunId
        scanner = $ScannerName
        artifact = [System.IO.Path]::GetFileName($ArtifactPath)
        sha256 = $hash
        generatedAt = (Get-Date).ToString("o")
    }
    $metaPath = "$ArtifactPath.meta.json"
    [System.IO.File]::WriteAllText($metaPath, ($meta | ConvertTo-Json -Compress), [System.Text.UTF8Encoding]::new($false))
}

$scannersToRun = if ($Scanner -eq "All") { @("Gitleaks", "Trivy", "Syft", "Grype", "Summary") } else { @($Scanner) }

foreach ($s in $scannersToRun) {
    switch ($s) {
        "Gitleaks" {
            $t = Find-StrictTool "gitleaks"
            $reportPath = Join-Path $evidenceFullDir "gitleaks-report.json"
            Write-Host "Running Gitleaks $($t.Version) repository & commit scan..." -ForegroundColor Cyan

            # Prefer git subcommand (full history scan); fall back to detect --source for non-git environments.
            $gitleaksConfig = Join-Path $repoRoot ".gitleaks.toml"
            $glArgs = @("git", "--no-banner", "--report-format", "json", "--report-path", "`"$reportPath`"", "--exit-code", "1")
            if (Test-Path $gitleaksConfig) {
                $glArgs += @("--config", "`"$gitleaksConfig`"")
            }
            $proc = Start-Process -FilePath $t.Path -ArgumentList $glArgs -NoNewWindow -PassThru
            [void]($proc | Wait-Process -Timeout 120 -ErrorAction Stop)
            $exitCode = $proc.ExitCode

            if ($exitCode -ne 0) {
                throw "Gitleaks detected secret leaks in repository! (Exit code: $exitCode)"
            }
            if (-not (Test-Path $reportPath)) {
                throw "Gitleaks report file not generated at: $reportPath"
            }
            $glJson = Get-Content $reportPath -Raw | ConvertFrom-Json
            $leakCount = if ($glJson) { $glJson.Count } else { 0 }
            if ($leakCount -ne 0) {
                throw "Gitleaks report contains $leakCount leak findings."
            }
            Write-RunSidecar -ArtifactPath $reportPath -ScannerName "gitleaks"
            Write-Host "PASS: Gitleaks scan clean (0 leaks)." -ForegroundColor Green
        }

        "Trivy" {
            $t = Find-StrictTool "trivy"
            $configReport = Join-Path $evidenceFullDir "trivy-config-report.json"
            $imageReport = Join-Path $evidenceFullDir "trivy-image-report.json"

            Write-Host "Running Trivy $($t.Version) configuration scan..." -ForegroundColor Cyan
            $proc = Start-Process -FilePath $t.Path -ArgumentList "config", "`"$repoRoot`"", "--skip-dirs", "`.tools", "--format", "json", "--output", "`"$configReport`"", "--severity", "HIGH,CRITICAL", "--exit-code", "1", "--skip-version-check" -NoNewWindow -PassThru
            [void]($proc | Wait-Process -Timeout 180 -ErrorAction Stop)
            if ($proc.ExitCode -ne 0) {
                throw "Trivy detected HIGH/CRITICAL misconfigurations! (Exit code: $($proc.ExitCode))"
            }
            if (-not (Test-Path $configReport)) {
                throw "Trivy config report file not generated at: $configReport"
            }
            $cfgJson = Get-Content $configReport -Raw | ConvertFrom-Json
            $misconfCount = 0
            if ($cfgJson.Results) {
                foreach ($r in $cfgJson.Results) {
                    if ($r.Misconfigurations) { $misconfCount += $r.Misconfigurations.Count }
                }
            }
            if ($misconfCount -ne 0) {
                throw "Trivy config report contains $misconfCount HIGH/CRITICAL misconfigurations."
            }

            # Deliverable image scan is mandatory
            $dockerImg = "dxos-api:0.1.0-spike"
            $hasImage = (docker images -q $dockerImg 2>$null)
            if (-not $hasImage) {
                Write-Host "Deliverable image '$dockerImg' not found in Docker daemon; building deterministically..." -ForegroundColor Yellow
                & docker build -t $dockerImg $repoRoot
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to build deliverable image '$dockerImg' for mandatory security scanning."
                }
            }

            Write-Host "Running Trivy $($t.Version) image scan on $dockerImg..." -ForegroundColor Cyan
            $proc = Start-Process -FilePath $t.Path -ArgumentList "image", $dockerImg, "--format", "json", "--output", "`"$imageReport`"", "--severity", "HIGH,CRITICAL", "--exit-code", "1", "--skip-version-check" -NoNewWindow -PassThru
            [void]($proc | Wait-Process -Timeout 180 -ErrorAction Stop)
            if ($proc.ExitCode -ne 0) {
                throw "Trivy detected HIGH/CRITICAL vulnerabilities in image '$dockerImg'! (Exit code: $($proc.ExitCode))"
            }
            if (-not (Test-Path $imageReport)) {
                throw "Trivy image report file not generated at: $imageReport"
            }
            $imgJson = Get-Content $imageReport -Raw | ConvertFrom-Json
            $imgVulnCount = 0
            if ($imgJson.Results) {
                foreach ($r in $imgJson.Results) {
                    if ($r.Vulnerabilities) { $imgVulnCount += $r.Vulnerabilities.Count }
                }
            }
            if ($imgVulnCount -ne 0) {
                throw "Trivy image report contains $imgVulnCount HIGH/CRITICAL vulnerabilities."
            }
            Write-RunSidecar -ArtifactPath $configReport -ScannerName "trivy-config"
            Write-RunSidecar -ArtifactPath $imageReport -ScannerName "trivy-image"
            Write-Host "PASS: Trivy config and image scans clean (0 misconfigs, 0 vulnerabilities)." -ForegroundColor Green
        }

        "Syft" {
            $t = Find-StrictTool "syft"
            $committedSbom = Join-Path $repoRoot "artifacts\sbom.cdx.json"
            $evidenceSbom = Join-Path $evidenceFullDir "sbom.cdx.json"

            $stagingDir = Join-Path $ToolsDir ".staging\publish"
            if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
            [void](New-Item -ItemType Directory -Path $stagingDir -Force)

            $rawTempSbom = Join-Path $ToolsDir ".staging\raw-sbom.cdx.json"

            try {
                Write-Host "Publishing DX-OS deliverable for SBOM extraction..." -ForegroundColor Cyan
                & dotnet publish (Join-Path $repoRoot "src/DXOS.Api/DXOS.Api.csproj") -c Release --no-restore -o $stagingDir
                if ($LASTEXITCODE -ne 0) {
                    throw "Failed to publish DXOS.Api for SBOM extraction."
                }

                Write-Host "Generating CycloneDX SBOM using Syft $($t.Version)..." -ForegroundColor Cyan
                $proc = Start-Process -FilePath $t.Path -ArgumentList "dir:`"$stagingDir`"", "--source-name", "dxos-api", "--source-version", "0.1.0-spike", "-o", "cyclonedx-json=`"$rawTempSbom`"" -NoNewWindow -PassThru
                [void]($proc | Wait-Process -Timeout 60 -ErrorAction Stop)
                if ($proc.ExitCode -ne 0) {
                    throw "Syft SBOM generation failed with exit code $($proc.ExitCode)"
                }

                if (-not (Test-Path $rawTempSbom)) {
                    throw "Raw SBOM file was not generated at: $rawTempSbom"
                }

                # Normalize and deduplicate components by canonical identity (purl if present, else type:name@version)
                $rawJson = Get-Content $rawTempSbom -Raw | ConvertFrom-Json
                $seenComponents = @{}
                $dedupedComponents = @()

                foreach ($comp in $rawJson.components) {
                    $purl = if ($comp.purl) { $comp.purl.ToLowerInvariant() } else { "" }
                    $canonicalKey = if ($purl) { $purl } else { "$($comp.type):$($comp.name)@$($comp.version)".ToLowerInvariant() }

                    if (-not $seenComponents.ContainsKey($canonicalKey)) {
                        $seenComponents[$canonicalKey] = $true
                        $dedupedComponents += $comp
                    }
                }

                $rawJson.components = $dedupedComponents
                $finalSbomJson = $rawJson | ConvertTo-Json -Depth 15

                [System.IO.File]::WriteAllText($committedSbom, $finalSbomJson, [System.Text.UTF8Encoding]::new($false))
                [System.IO.File]::WriteAllText($evidenceSbom, $finalSbomJson, [System.Text.UTF8Encoding]::new($false))
            } finally {
                if (Test-Path $stagingDir) { Remove-Item $stagingDir -Recurse -Force }
                if (Test-Path $rawTempSbom) { Remove-Item $rawTempSbom -Force }
            }

            # Strict SBOM validation
            $sbomJson = Get-Content $committedSbom -Raw | ConvertFrom-Json
            if ($sbomJson.bomFormat -ne "CycloneDX") {
                throw "Invalid SBOM format: $($sbomJson.bomFormat) (expected CycloneDX)"
            }
            if (-not $sbomJson.components -or $sbomJson.components.Count -eq 0) {
                throw "SBOM generated with 0 components."
            }
            if (-not $sbomJson.metadata -or -not $sbomJson.metadata.component -or $sbomJson.metadata.component.name -ne "dxos-api") {
                throw "SBOM metadata component does not identify deliverable 'dxos-api'."
            }

            $rawSbomText = Get-Content $committedSbom -Raw
            if ($rawSbomText -match "C:\\\\Users|/Users/|OneDrive") {
                throw "SBOM contains private machine path leak!"
            }

            Write-RunSidecar -ArtifactPath $evidenceSbom -ScannerName "syft"
            Write-Host "PASS: Syft generated canonical deduplicated CycloneDX SBOM ($($sbomJson.components.Count) components)." -ForegroundColor Green
        }

        "Grype" {
            $t = Find-StrictTool "grype"
            $committedSbom = Join-Path $repoRoot "artifacts\sbom.cdx.json"
            $reportPath = Join-Path $evidenceFullDir "grype-report.json"

            if (-not (Test-Path $committedSbom)) {
                throw "SBOM not found at $committedSbom. Run Syft first."
            }

            Write-Host "Running Grype $($t.Version) vulnerability scan on SBOM..." -ForegroundColor Cyan
            $arg1 = "sbom:`"$committedSbom`""
            $proc = Start-Process -FilePath $t.Path -ArgumentList $arg1, "--fail-on", "high", "--output", "json" -NoNewWindow -PassThru -RedirectStandardOutput $reportPath
            [void]($proc | Wait-Process -Timeout 120 -ErrorAction Stop)
            $grypeExit = $proc.ExitCode

            if ($grypeExit -ne 0) {
                throw "Grype vulnerability scan failed policy check (Exit code: $grypeExit)"
            }
            if (-not (Test-Path $reportPath)) {
                throw "Grype report file was not generated at: $reportPath"
            }

            $grypeJson = Get-Content $reportPath -Raw | ConvertFrom-Json
            $matchCount = if ($grypeJson.matches) { $grypeJson.matches.Count } else { 0 }
            if ($matchCount -ne 0) {
                throw "Grype report contains $matchCount HIGH/CRITICAL vulnerability matches."
            }
            Write-RunSidecar -ArtifactPath $reportPath -ScannerName "grype"
            Write-Host "PASS: Grype scan clean (0 vulnerabilities at/above threshold)." -ForegroundColor Green
        }

        "Summary" {
            Write-Host "Running atomic security summary generation..." -ForegroundColor Cyan
            $pwshExe = (Get-Process -Id $PID).Path
            & $pwshExe -NoProfile -ExecutionPolicy Bypass -File (Join-Path $PSScriptRoot "generate-security-summary.ps1") -EvidenceDir $evidenceFullDir -ToolsDir $ToolsDir -RunId $RunId
            if ($LASTEXITCODE -ne 0) {
                throw "generate-security-summary.ps1 failed with exit code $LASTEXITCODE"
            }
        }
    }
}
