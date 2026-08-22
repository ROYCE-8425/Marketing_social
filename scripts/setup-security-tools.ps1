[CmdletBinding()]
param(
    [string]$Tool = "",
    [string]$ManifestPath = "",
    [string]$ToolsDir = "",
    [switch]$Force,
    [switch]$VerifyOnly,
    [string]$ValidateTarArchive = "",
    [string]$ValidateTarExtractTarget = ""
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue

$repoRoot = (Get-Item (Join-Path $PSScriptRoot "..")).FullName

if (-not $ManifestPath) {
    $ManifestPath = Join-Path $PSScriptRoot "security-tools.json"
}

if (-not (Test-Path $ManifestPath)) {
    throw "Security tools manifest not found at: $ManifestPath"
}

$manifestJson = Get-Content $ManifestPath -Raw | ConvertFrom-Json

if (-not $ToolsDir) {
    $ToolsDir = Join-Path $repoRoot ".tools\security"
}
$ToolsDir = [System.IO.Path]::GetFullPath($ToolsDir)

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

if (-not (Test-Path $ToolsDir)) {
    [void](New-Item -ItemType Directory -Path $ToolsDir -Force)
}
[void](Assert-SafePathChain $ToolsDir $repoRoot)

# Determine OS platform key
$platform = "windows-x64"
if ($IsLinux) {
    $platform = "linux-x64"
} elseif ($IsMacOS) {
    throw "macOS is not a supported runner platform for pinned security tools in DX-OS"
}

$toolsToProcess = $manifestJson.tools
if ($Tool) {
    $toolsToProcess = $manifestJson.tools | Where-Object { $_.name -eq $Tool }
    if (-not $toolsToProcess) {
        throw "Tool '$Tool' not found in manifest."
    }
}

function Verify-ExecutableBinary {
    param(
        [string]$ExePath,
        [string]$ToolName,
        [string]$ExpectedVersion,
        [string]$ExpectedExeSha,
        [string[]]$ForbiddenVersions
    )

    if (-not (Test-Path $ExePath)) {
        return @{ Valid = $false; Error = "File not found: $ExePath" }
    }

    [void](Assert-SafePathChain $ExePath $ToolsDir)

    # Check forbidden versions
    if ($ForbiddenVersions -and ($ForbiddenVersions -contains $ExpectedVersion)) {
        throw "Tool '$ToolName' version '$ExpectedVersion' is explicitly FORBIDDEN by security policy."
    }

    # Verify SHA-256
    if ($ExpectedExeSha) {
        $actualHash = (Get-FileHash -Path $ExePath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualHash -ne $ExpectedExeSha.ToLowerInvariant()) {
            return @{ Valid = $false; Error = "Executable SHA-256 mismatch for $ToolName (Expected: $ExpectedExeSha, Actual: $actualHash)" }
        }
    }

    # Verify version output
    $verOut = ""
    $verExit = -1
    $proc = $null
    try {
        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $ExePath
        $psi.Arguments = if ($ToolName -eq "gitleaks") { "version" } else { "--version" }
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true

        $proc = New-Object System.Diagnostics.Process
        $proc.StartInfo = $psi
        [void]$proc.Start()
        $outTask = $proc.StandardOutput.ReadToEndAsync()
        $errTask = $proc.StandardError.ReadToEndAsync()

        $completed = $proc.WaitForExit(10000)
        if (-not $completed) {
            try { $proc.Kill() } catch {}
            return @{ Valid = $false; Error = "Version verification timed out for $ToolName" }
        }
        [void][Threading.Tasks.Task]::WaitAll($outTask, $errTask)
        $verOut = $outTask.Result + "`n" + $errTask.Result
        $verExit = $proc.ExitCode

        if ($verExit -ne 0 -and $ToolName -ne "gitleaks") {
            # Fallback to 'version' subcommand if --version failed
            $psi.Arguments = "version"
            $proc = New-Object System.Diagnostics.Process
            $proc.StartInfo = $psi
            [void]$proc.Start()
            $outTask = $proc.StandardOutput.ReadToEndAsync()
            $errTask = $proc.StandardError.ReadToEndAsync()
            $completed = $proc.WaitForExit(10000)
            if (-not $completed) {
                try { $proc.Kill() } catch {}
                return @{ Valid = $false; Error = "Version verification timed out for $ToolName" }
            }
            [void][Threading.Tasks.Task]::WaitAll($outTask, $errTask)
            $verOut = $outTask.Result + "`n" + $errTask.Result
            $verExit = $proc.ExitCode
        }
    } finally {
        if ($null -ne $proc) { $proc.Dispose() }
    }

    if ($verExit -ne 0) {
        return @{ Valid = $false; Error = "Tool $ToolName version command failed with exit code $verExit" }
    }

    if (-not ($verOut -match [regex]::Escape($ExpectedVersion))) {
        return @{ Valid = $false; Error = "Tool $ToolName reported version mismatch: expected '$ExpectedVersion' in output: $verOut" }
    }

    return @{ Valid = $true; VersionOutput = $verOut.Trim() }
}

function Validate-TarArchiveSafely {
    param(
        [string]$ArchiveFile,
        [string]$ExtractTarget
    )

    $stagingWithSep = [System.IO.Path]::GetFullPath($ExtractTarget)
    if (-not $stagingWithSep.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
        $stagingWithSep += [System.IO.Path]::DirectorySeparatorChar
    }

    # 1. Enumerate entry names
    $entries = & tar -tf $ArchiveFile 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to list entries in tar archive '$ArchiveFile'"
    }

    foreach ($rawEntry in ($entries -split "`r?`n")) {
        $entryName = $rawEntry.Trim()
        if ([string]::IsNullOrWhiteSpace($entryName)) { continue }

        # Check for path traversal, absolute path, drive letter
        if ($entryName.Contains("..") -or $entryName.StartsWith("/") -or $entryName.StartsWith("\") -or ($entryName -match '^[a-zA-Z]:')) {
            throw "Tar entry traversal or absolute path violation: '$entryName'"
        }

        $destPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($ExtractTarget, $entryName))
        if (-not $destPath.StartsWith($stagingWithSep, [System.StringComparison]::OrdinalIgnoreCase) -and -not $destPath.Equals([System.IO.Path]::GetFullPath($ExtractTarget), [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Tar entry path escape detected: '$entryName' -> '$destPath'"
        }
    }

    # 2. Detailed check for symlinks and hardlinks
    $detailedEntries = & tar -tvf $ArchiveFile 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to list detailed entries in tar archive '$ArchiveFile'"
    }

    foreach ($rawLine in ($detailedEntries -split "`r?`n")) {
        $line = $rawLine.Trim()
        if ([string]::IsNullOrWhiteSpace($line)) { continue }

        # Reject symlinks and hardlinks (mode starting with l or h, or containing link indicators)
        if ($line -match '^[lh]' -or $line -match '\s+->\s+' -or $line -match '\s+link to\s+') {
            throw "Tar link escape violation (symlinks and hardlinks forbidden): '$line'"
        }
    }
}

if (-not [string]::IsNullOrWhiteSpace($ValidateTarArchive)) {
    if ([string]::IsNullOrWhiteSpace($ValidateTarExtractTarget)) {
        throw "ValidateTarExtractTarget is required when ValidateTarArchive is set."
    }
    if (-not (Test-Path $ValidateTarArchive)) {
        throw "TAR archive not found: $ValidateTarArchive"
    }
    if (-not (Test-Path $ValidateTarExtractTarget)) {
        [void](New-Item -ItemType Directory -Path $ValidateTarExtractTarget -Force)
    }
    Validate-TarArchiveSafely -ArchiveFile $ValidateTarArchive -ExtractTarget $ValidateTarExtractTarget
    Write-Host "PASS: TAR archive entries validated before extraction."
    return
}

$stagingBase = Join-Path $ToolsDir ".staging"
if (-not (Test-Path $stagingBase)) {
    [void](New-Item -ItemType Directory -Path $stagingBase -Force)
}

foreach ($toolEntry in $toolsToProcess) {
    $toolName = $toolEntry.name
    $toolVer = $toolEntry.version
    $forbidden = $toolEntry.forbiddenVersions

    if ($forbidden -and ($forbidden -contains $toolVer)) {
        throw "Tool '$toolName' version '$toolVer' is explicitly FORBIDDEN by security policy."
    }

    $artifactInfo = $toolEntry.artifacts.$platform
    if (-not $artifactInfo) {
        throw "No artifact defined for tool '$toolName' on platform '$platform'."
    }

    $destRelPath = $artifactInfo.cacheRelativePath
    $targetExePath = Join-Path $ToolsDir $destRelPath
    $targetExeDir = Split-Path $targetExePath -Parent

    if (-not (Test-Path $targetExeDir)) {
        [void](New-Item -ItemType Directory -Path $targetExeDir -Force)
    }

    if (-not $Force -and (Test-Path $targetExePath)) {
        $check = Verify-ExecutableBinary -ExePath $targetExePath -ToolName $toolName -ExpectedVersion $toolVer -ExpectedExeSha $artifactInfo.executableSha256 -ForbiddenVersions $forbidden
        if ($check.Valid) {
            Write-Host "Tool '$toolName' ($toolVer) is verified and ready." -ForegroundColor Green
            continue
        } else {
            if ($VerifyOnly) {
                throw "Tool verification failed for '$toolName': $($check.Error)"
            }
            Write-Host "Existing binary invalid ($($check.Error)), re-acquiring..." -ForegroundColor Yellow
        }
    }

    if ($VerifyOnly) {
        throw "Tool '$toolName' ($toolVer) not found at: $targetExePath"
    }

    $stagingGuid = [guid]::NewGuid().ToString("N")
    $stagingDir = Join-Path $stagingBase $stagingGuid
    [void](New-Item -ItemType Directory -Path $stagingDir -Force)
    $tempArchive = Join-Path $stagingDir "archive-$toolName-$toolVer.tmp"

    try {
        Write-Host "Downloading $toolName $toolVer from $($artifactInfo.url)..." -ForegroundColor Cyan
        Invoke-WebRequest -Uri $artifactInfo.url -OutFile $tempArchive -TimeoutSec 60 -UseBasicParsing

        # Validate Archive SHA-256
        $computedHash = (Get-FileHash -Path $tempArchive -Algorithm SHA256).Hash.ToLowerInvariant()
        $expectedHash = $artifactInfo.sha256.ToLowerInvariant()
        if ($computedHash -ne $expectedHash) {
            throw "ARCHIVE INTEGRITY FAILURE for $toolName $toolVer! Computed: $computedHash, Expected: $expectedHash"
        }
        Write-Host "Archive SHA-256 verified for $toolName" -ForegroundColor Green

        $extractTarget = Join-Path $stagingDir "extracted"
        [void](New-Item -ItemType Directory -Path $extractTarget -Force)
        $stagingWithSep = $extractTarget
        if (-not $stagingWithSep.EndsWith([System.IO.Path]::DirectorySeparatorChar.ToString())) {
            $stagingWithSep += [System.IO.Path]::DirectorySeparatorChar
        }

        if ($artifactInfo.archiveType -eq "zip") {
            $zip = [System.IO.Compression.ZipFile]::OpenRead($tempArchive)
            try {
                foreach ($entry in $zip.Entries) {
                    $entryName = $entry.FullName
                    if ($entryName.Contains("..") -or $entryName.StartsWith("/") -or $entryName.StartsWith("\") -or ($entryName -match '^[a-zA-Z]:')) {
                        throw "Zip-Slip attempt detected in archive entry: $entryName"
                    }
                    $destPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($extractTarget, $entryName))
                    if (-not $destPath.StartsWith($stagingWithSep, [System.StringComparison]::OrdinalIgnoreCase) -and -not $destPath.Equals($extractTarget, [System.StringComparison]::OrdinalIgnoreCase)) {
                        throw "Zip-Slip path escape detected in archive entry: $entryName"
                    }
                    if ($entry.Length -eq 0 -and $entry.CompressedLength -eq 0) {
                        # Directory entry
                        if (-not (Test-Path $destPath)) { [void](New-Item -ItemType Directory -Path $destPath -Force) }
                    } else {
                        $parentDir = [System.IO.Path]::GetDirectoryName($destPath)
                        if (-not (Test-Path $parentDir)) { [void](New-Item -ItemType Directory -Path $parentDir -Force) }
                        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entry, $destPath, $true)
                    }
                }
            } finally {
                $zip.Dispose()
            }
        } elseif ($artifactInfo.archiveType -eq "tar.gz") {
            Validate-TarArchiveSafely -ArchiveFile $tempArchive -ExtractTarget $extractTarget
            & tar -xzf $tempArchive -C $extractTarget
            if ($LASTEXITCODE -ne 0) {
                throw "Tar extraction failed for $toolName $toolVer"
            }
        }

        # Locate executable
        $exeName = $artifactInfo.executable
        $foundExe = Get-ChildItem -Path $extractTarget -Filter $exeName -Recurse -File | Select-Object -First 1
        if (-not $foundExe) {
            throw "Executable '$exeName' not found inside extracted archive for $toolName."
        }

        # Validate extracted executable SHA-256
        if ($artifactInfo.executableSha256) {
            $extractedHash = (Get-FileHash -Path $foundExe.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            if ($extractedHash -ne $artifactInfo.executableSha256.ToLowerInvariant()) {
                throw "EXECUTABLE INTEGRITY FAILURE for $toolName $toolVer! Computed: $extractedHash, Expected: $($artifactInfo.executableSha256)"
            }
        }

        # Stage to target location
        Copy-Item -Path $foundExe.FullName -Destination $targetExePath -Force
        [void](Assert-SafePathChain $targetExePath $ToolsDir)

        # Final verification
        $finalCheck = Verify-ExecutableBinary -ExePath $targetExePath -ToolName $toolName -ExpectedVersion $toolVer -ExpectedExeSha $artifactInfo.executableSha256 -ForbiddenVersions $forbidden
        if (-not $finalCheck.Valid) {
            throw "Post-installation verification failed for $($toolName): $($finalCheck.Error)"
        }
        Write-Host "Successfully installed and verified $toolName $toolVer" -ForegroundColor Green
    } finally {
        if (Test-Path $stagingDir) {
            Remove-Item -Path $stagingDir -Recurse -Force
        }
    }
}

# Clean staging base if empty
if (Test-Path $stagingBase) {
    $remaining = Get-ChildItem -Path $stagingBase
    if ($remaining.Count -eq 0) {
        Remove-Item -Path $stagingBase -Recurse -Force
    }
}

Write-Host "`nAll security tools setup complete." -ForegroundColor Green
