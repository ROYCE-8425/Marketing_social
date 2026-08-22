# Finalizer Atomic Publication Helper for BR001-R5

function Assert-SafePathChain {
    param(
        [string]$Path,
        [string]$ExpectedRoot
    )
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $fullRoot = [System.IO.Path]::GetFullPath($ExpectedRoot)
    if (-not $fullPath.StartsWith($fullRoot, [System.StringComparison]::OrdinalIgnoreCase) -or $fullPath.Length -le $fullRoot.Length) {
        throw "Security violation: '$fullPath' is not a strict descendant of '$fullRoot'"
    }
    $curr = Get-Item -LiteralPath $fullPath -Force -ErrorAction SilentlyContinue
    if ($null -eq $curr) {
        $curr = (Get-Item -LiteralPath (Split-Path $fullPath -Parent) -Force)
    }
    while ($curr -ne $null -and $curr.FullName.Length -ge $fullRoot.Length) {
        if ($curr.Attributes -band [System.IO.FileAttributes]::ReparsePoint) {
            throw "Security violation: Reparse point detected in path chain at '$($curr.FullName)'"
        }
        $curr = $curr.Parent
    }
}

$script:utf8Strict = [System.Text.UTF8Encoding]::new($false, $true)

function Test-StrictFileHygiene {
    param([string]$FilePath)
    $rawBytes = [System.IO.File]::ReadAllBytes($FilePath)

    try {
        $text = $script:utf8Strict.GetString($rawBytes)
    } catch {
        return "Invalid UTF-8 encoding: $($_.Exception.Message)"
    }

    for ($ci = 0; $ci -lt $text.Length; $ci++) {
        $cVal = [int][char]$text[$ci]
        if ($cVal -eq 0xFFFD) {
            return "Contains U+FFFD replacement character at offset $ci"
        }
        if ($cVal -lt 0x20 -and $cVal -ne 0x09 -and $cVal -ne 0x0A -and $cVal -ne 0x0D) {
            return "Contains forbidden control character (0x$($cVal.ToString('X2'))) at offset $ci"
        }
    }

    if ($text -match '[\u00C2\u00C3\u00E2][\u0080-\u00BF]') {
        return "Contains literal mojibake sequence"
    }

    $lines = $text -split "`r?`n"
    for ($li = 0; $li -lt $lines.Length; $li++) {
        if ($lines[$li] -match '[ \t]+$') {
            return "Contains trailing whitespace on line $($li + 1)"
        }
    }

    return $null
}

function Write-CleanFile {
    param(
        [string]$FilePath,
        [string]$Content
    )
    $cleanLines = @()
    foreach ($line in ($Content -split "`r?`n")) {
        $cleanLines += $line.TrimEnd()
    }
    $normalized = $cleanLines -join "`r`n"
    [System.IO.File]::WriteAllText($FilePath, $normalized, [System.Text.UTF8Encoding]::new($false))
}

function Publish-CompletionUnit {
    param(
        [string]$RunDir,
        [string]$CompletionLogContent,
        [string]$RelLogPath,
        [scriptblock]$ProjectedGitValidator = $null
    )

    $stagingDir = Join-Path $RunDir ".completion-staging"
    $finalCompletionDir = Join-Path $RunDir "completion"

    if (Test-Path $stagingDir) {
        Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction Stop
        if (Test-Path $stagingDir) {
            throw "Fail-closed cleanup error: Unable to remove existing staging directory at '$stagingDir'"
        }
    }

    $moveExecuted = $false
    try {
        New-Item -ItemType Directory -Path $stagingDir -Force -ErrorAction Stop | Out-Null
        Assert-SafePathChain $stagingDir $RunDir

        $stagedLogPath = Join-Path $stagingDir "finalizer-completion.log"
        $stagedShaPath = Join-Path $stagingDir "finalizer-completion.sha256"

        Write-CleanFile $stagedLogPath $CompletionLogContent

        $logHash = (Get-FileHash -Path $stagedLogPath -Algorithm SHA256).Hash.ToLowerInvariant()
        Write-CleanFile $stagedShaPath "$logHash  $RelLogPath"

        # Staging validation: hygiene
        $err1 = Test-StrictFileHygiene $stagedLogPath
        if ($null -ne $err1) { throw "Staged completion log hygiene error: $err1" }
        $err2 = Test-StrictFileHygiene $stagedShaPath
        if ($null -ne $err2) { throw "Staged completion sha hygiene error: $err2" }

        # Staging validation: checksum
        $actualSha = (Get-FileHash -Path $stagedLogPath -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualSha -ne $logHash) {
            throw "Staged completion checksum mismatch (Expected $logHash, got $actualSha)"
        }

        # Projected Git validation before moving
        if ($null -ne $ProjectedGitValidator) {
            & $ProjectedGitValidator
        }

        # Final destination safety check
        if (Test-Path $finalCompletionDir) {
            throw "Atomic publication error: Destination completion directory '$finalCompletionDir' already exists"
        }

        # Atomic Rename / Move
        [System.IO.Directory]::Move($stagingDir, $finalCompletionDir)
        $moveExecuted = $true

        if (Test-Path $stagingDir) {
            throw "Atomic publication error: Staging directory '$stagingDir' still exists after rename"
        }
        if (-not (Test-Path $finalCompletionDir)) {
            throw "Atomic publication error: Final completion directory '$finalCompletionDir' missing after rename"
        }

        $finalFiles = Get-ChildItem -Path $finalCompletionDir -File
        if ($finalFiles.Count -ne 2) {
            throw "Atomic publication error: Expected exactly 2 files in '$finalCompletionDir', found $($finalFiles.Count)"
        }
    } catch {
        # Fail-closed cleanup: do not suppress errors, assert absence
        if (Test-Path $stagingDir) {
            Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction Stop
            if (Test-Path $stagingDir) {
                throw "Fail-closed cleanup error: Failed to purge staging directory on failure: '$stagingDir'"
            }
        }
        if ($moveExecuted -and (Test-Path $finalCompletionDir)) {
            Remove-Item -Path $finalCompletionDir -Recurse -Force -ErrorAction Stop
            if (Test-Path $finalCompletionDir) {
                throw "Fail-closed cleanup error: Failed to purge completion directory on failure: '$finalCompletionDir'"
            }
        }
        throw
    }
}
