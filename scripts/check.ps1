param(
    [string]$Profile = "Full",
    [string]$ContractPath = "scripts/check-contract.json",
    [string]$EvidencePath = ""
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path ".git")) {
    [Console]::Error.WriteLine("check.ps1 must be executed from the repository root")
    exit 1
}
$expectedRoot = (Get-Item .).FullName

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

[void](Assert-SafePathChain $expectedRoot $expectedRoot)

function Get-SafeChildPath {
    param([string]$Root, [string]$RelativePath, [string]$Name)
    if ([string]::IsNullOrWhiteSpace($RelativePath)) { throw "$Name path is empty." }
    if ([System.IO.Path]::IsPathRooted($RelativePath)) { throw "$Name path '$RelativePath' must not be rooted." }
    if ($RelativePath -match '\.\.') { throw "$Name path '$RelativePath' must not contain '..'." }

    $normRel = $RelativePath.Replace('\', [System.IO.Path]::DirectorySeparatorChar).Replace('/', [System.IO.Path]::DirectorySeparatorChar)
    $fullPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($Root, $normRel))
    [void](Assert-SafePathChain $fullPath $Root)
    return $fullPath
}

$sdkVersion = (dotnet --version).Trim()
if ($sdkVersion -notmatch '^10\.0\.(3[0-9]{2})$') {
    throw "Expected .NET SDK 10.0.3xx (pinned 10.0.302, rollForward latestPatch), found $sdkVersion"
}
$sdkFeaturePatch = [int]$Matches[1]
if ($sdkFeaturePatch -lt 302) {
    throw "Expected .NET SDK >= 10.0.302, found $sdkVersion"
}

if (-not (Get-Command "openspec" -ErrorAction SilentlyContinue) -and -not (Get-Command "openspec.cmd" -ErrorAction SilentlyContinue)) {
    throw "openspec CLI is not available in PATH."
}

$expectedLocks = @(
    "src/DXOS.Api/packages.lock.json",
    "src/DXOS.AppHost/packages.lock.json",
    "src/DXOS.Application/packages.lock.json",
    "src/DXOS.Domain/packages.lock.json",
    "src/DXOS.Infrastructure/packages.lock.json",
    "src/DXOS.Workflows/packages.lock.json",
    "tests/DXOS.Architecture.Tests/packages.lock.json",
    "tests/DXOS.Integration.Tests/packages.lock.json",
    "tests/DXOS.Unit.Tests/packages.lock.json"
)

function Get-LockSnapshot {
    $snapshot = @{}
    foreach ($lock in $expectedLocks) {
        $fullPath = Get-SafeChildPath $expectedRoot $lock "Lock"
        if (-not (Test-Path $fullPath)) {
            throw "Missing required lock file: $lock"
        }
        $file = Get-Item $fullPath
        $hash = (Get-FileHash -Path $fullPath -Algorithm SHA256).Hash
        $snapshot[$lock] = @{ Length = $file.Length; Hash = $hash }
    }

    $allLocks = Get-ChildItem -Path $expectedRoot -Filter "packages.lock.json" -Recurse
    if ($allLocks.Count -ne 9) {
        throw "Expected exactly 9 packages.lock.json files, found $($allLocks.Count)"
    }
    return $snapshot
}

$preLocks = Get-LockSnapshot

$disposablePostgresSecret = $false
if ([string]::IsNullOrWhiteSpace($env:POSTGRES_PASSWORD)) {
    $env:POSTGRES_PASSWORD = [Guid]::NewGuid().ToString('N')
    $disposablePostgresSecret = $true
}

$contractFullPath = Get-SafeChildPath $expectedRoot $ContractPath "Contract"
if (-not (Test-Path $contractFullPath)) {
    throw "Missing contract file: $contractFullPath"
}

$contract = Get-Content $contractFullPath -Raw | ConvertFrom-Json

$validProfiles = $contract.profiles.PSObject.Properties.Name
if ($validProfiles -notcontains $Profile) {
    throw "Invalid profile '$Profile'. Must be one of: $($validProfiles -join ', ')"
}

$runId = [guid]::NewGuid().ToString()

if ([string]::IsNullOrWhiteSpace($EvidencePath)) {
    $outDir = Get-SafeChildPath $expectedRoot "artifacts/quality-gate/run-$runId" "Quality Gate Dir"
    if (-not (Test-Path $outDir)) {
        New-Item -ItemType Directory -Path $outDir -Force | Out-Null
    }
    $evidenceFile = Get-SafeChildPath $outDir "evidence-${Profile}-${runId}.json" "Evidence file"
} else {
    $evidenceFile = Get-SafeChildPath $expectedRoot $EvidencePath "Evidence file"
    $evidenceDir = [System.IO.Path]::GetDirectoryName($evidenceFile)
    if (-not (Test-Path $evidenceDir)) {
        New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null
    }
    [void](Assert-SafePathChain $evidenceDir $expectedRoot)
    $outDir = $evidenceDir
}

$gatesDef = $contract.gates
$profileDef = $contract.profiles.$Profile

$gateMap = @{}
foreach ($g in $gatesDef) {
    $gateMap[$g.id] = $g
}

$selectedGateIds = @()
if ($null -ne $profileDef -and ($profileDef.PSObject.Properties.Name -contains "gates")) {
    $selectedGateIds = $profileDef.gates
} else {
    foreach ($g in $gatesDef) {
        if ($g.profiles -contains $Profile) {
            $selectedGateIds += $g.id
        }
    }
}

$selectedGates = @()
$seenIds = @{}

foreach ($gateId in $selectedGateIds) {
    if (-not $gateMap.ContainsKey($gateId)) {
        throw "Profile '$Profile' references undefined gate '$gateId'."
    }
    $gate = $gateMap[$gateId]

    if ([string]::IsNullOrWhiteSpace($gate.id) -or
        [string]::IsNullOrWhiteSpace($gate.status) -or
        [string]::IsNullOrWhiteSpace($gate.task) -or
        $null -eq $gate.timeoutSeconds -or
        [string]::IsNullOrWhiteSpace($gate.activation)) {
        throw "Gate $($gate.id) missing required fields."
    }

    if ($gate.id -notmatch '^[a-zA-Z0-9_-]+$') { throw "Invalid gate ID: $($gate.id)" }
    if ($seenIds.ContainsKey($gate.id)) { throw "Duplicate gate ID: $($gate.id)" }
    $seenIds[$gate.id] = $true

    if ($gate.status -notin @('READY', 'NOT_IMPLEMENTED', 'NOT_APPLICABLE')) { throw "Invalid status: $($gate.status)" }
    if ($gate.activation -notin @('always', 'manual')) { throw "Invalid activation: $($gate.activation)" }
    if ($gate.timeoutSeconds -le 0) { throw "Invalid timeout: $($gate.timeoutSeconds)" }
    if ($gate.task -notmatch '^BR001-R[0-9](\.[0-9])?$') { throw "Malformed task ownership: $($gate.task)" }

    if ($gate.command -match '^(cmd|sh|bash)$' -and ($gate.arguments -match 'Invoke-Expression|^-[c]$|^/[c]$')) {
        throw "Unsafe shell construction in gate $($gate.id)"
    }

    if ($gate.status -eq 'READY') {
        if ([string]::IsNullOrWhiteSpace($gate.command)) { throw "READY gate $($gate.id) missing executable definition." }
    } elseif ($gate.status -eq 'NOT_APPLICABLE') {
        if ([string]::IsNullOrWhiteSpace($gate.reason) -or [string]::IsNullOrWhiteSpace($gate.activationCondition)) {
            throw "NOT_APPLICABLE gate $($gate.id) missing reason or activationCondition."
        }
    } elseif ($gate.status -eq 'NOT_IMPLEMENTED') {
        if ($gate.command -match '^(echo|Write-Host)$' -or $gate.command -eq 'pass') {
            throw "NOT_IMPLEMENTED gate $($gate.id) contains fake executable PASS command."
        }
        if ([string]::IsNullOrWhiteSpace($gate.activationCondition)) {
            throw "NOT_IMPLEMENTED gate $($gate.id) missing actionable activationCondition."
        }
    }

    foreach ($file in $gate.requiredFiles) { [void](Get-SafeChildPath $expectedRoot $file "Required File") }
    foreach ($out in $gate.expectedOutputs) {
        $dummyOut = $out -replace '\{EvidenceDir\}', 'artifacts/quality-gate' -replace '\{EvidenceRoot\}', 'artifacts/quality-gate' -replace '\{RunId\}', 'dummy-run-id'
        [void](Get-SafeChildPath $expectedRoot $dummyOut "Expected Output")
    }

    $selectedGates += $gate
}

$evidence = @{
    schemaVersion = "1.0"
    runId = $runId
    profile = $Profile
    gitRevision = (git rev-parse HEAD).Trim()
    startTime = (Get-Date).ToString("o")
    repositoryRoot = $expectedRoot
    dotnetVersion = $sdkVersion
    resolvedTools = @{}
    gates = @()
    firstFailure = $null
    overallResult = "PASS"
    locks = $preLocks
    sanitizationPolicy = "Redact SECRET_KEY"
    sanitizationResult = "Applied"
}

# Preflight tools and files
$toolsSecurityDir = Join-Path $expectedRoot ".tools\security"
$secManifestPath = Join-Path $expectedRoot "scripts\security-tools.json"
$secManifest = if (Test-Path $secManifestPath) { Get-Content $secManifestPath -Raw | ConvertFrom-Json } else { $null }

$secToolNames = @("gitleaks", "trivy", "syft", "grype")

$platform = "windows-x64"
$exeExt = ".exe"
if ($IsLinux) {
    $platform = "linux-x64"
    $exeExt = ""
}

$resolvedTools = @{}
foreach ($gate in $selectedGates) {
    if ($gate.status -ne 'READY') { continue }
    foreach ($tool in $gate.requiredTools) {
        if (-not $resolvedTools.ContainsKey($tool)) {
            if ($secToolNames -contains $tool) {
                # Security tool resolution: strict local cached binary check
                if ($null -eq $secManifest) {
                    throw "scripts/security-tools.json not found for security tool '$tool'."
                }
                $toolDef = $secManifest.tools | Where-Object { $_.name -eq $tool } | Select-Object -First 1
                if (-not $toolDef) {
                    throw "Security tool '$tool' not declared in security-tools.json."
                }
                $art = $toolDef.artifacts.$platform
                $localExe = Join-Path $toolsSecurityDir "$tool/$($toolDef.version)/$tool$exeExt"
                if (-not (Test-Path $localExe)) {
                    throw "Missing required security tool binary: $localExe. Run scripts/setup-security-tools.ps1."
                }
                [void](Assert-SafePathChain $localExe $toolsSecurityDir)
                $hash = (Get-FileHash -Path $localExe -Algorithm SHA256).Hash.ToLowerInvariant()
                if ($art.executableSha256 -and ($hash -ne $art.executableSha256.ToLowerInvariant())) {
                    throw "Executable SHA-256 mismatch for security tool '$tool'."
                }
                $resolvedTools[$tool] = @{ Path = $localExe; Version = $toolDef.version; Sha256 = $hash }
            } elseif ($tool -eq "powershell" -or $tool -eq "powershell.exe" -or $tool -eq "pwsh") {
                $psCmd = if ($IsLinux) { Get-Command "pwsh" -ErrorAction SilentlyContinue } else { Get-Command "powershell.exe" -ErrorAction SilentlyContinue }
                if (-not $psCmd) {
                    $psCmd = Get-Command "pwsh" -ErrorAction SilentlyContinue
                }
                if (-not $psCmd) {
                    $psCmd = Get-Command "powershell" -ErrorAction SilentlyContinue
                }
                if (-not $psCmd) {
                    throw "PowerShell executable not found in PATH."
                }
                $resolvedTools[$tool] = @{ Path = $psCmd.Source; Version = $psCmd.Version.ToString() }
            } elseif ($tool -eq "openspec" -or $tool -eq "openspec.cmd") {
                $osCmd = if ($IsLinux) { Get-Command "openspec" -ErrorAction SilentlyContinue } else { Get-Command "openspec.cmd" -ErrorAction SilentlyContinue }
                if (-not $osCmd) {
                    $osCmd = Get-Command "openspec" -ErrorAction SilentlyContinue
                }
                if (-not $osCmd) {
                    throw "OpenSpec executable not found in PATH."
                }
                $resolvedTools[$tool] = @{ Path = $osCmd.Source; Version = "1.8.0" }
            } else {
                $cmd = Get-Command $tool -ErrorAction SilentlyContinue
                if (-not $cmd) {
                    $evidence.resolvedTools = $resolvedTools
                    $evidence.overallResult = "FAIL"
                    $evidence.firstFailure = $gate.id
                    $preOutFile = Get-SafeChildPath $outDir "${Profile}-$($gate.id).out.txt" "Preflight Output"
                    [IO.File]::WriteAllText($preOutFile, "Missing required tool: $tool for gate $($gate.id)", [System.Text.UTF8Encoding]::new($false))
                    $preOutHash = (Get-FileHash -Path $preOutFile -Algorithm SHA256).Hash

                    $evidence.gates += @{
                        id = $gate.id
                        status = $gate.status
                        task = $gate.task
                        startTime = (Get-Date).ToString("o")
                        durationSeconds = 0
                        exitCode = -1
                        outputHash = $preOutHash
                        error = "Missing required tool: $tool for gate $($gate.id)"
                        command = $gate.command
                        arguments = $gate.arguments
                        encodedInvocation = ""
                        outputPath = "${Profile}-$($gate.id).out.txt"
                        activation = $gate.activation
                        timeoutSeconds = $gate.timeoutSeconds
                        processState = "preflight-failure"
                    }
                    $evidence.endTime = (Get-Date).ToString("o")
                    [System.IO.File]::WriteAllText($evidenceFile, ($evidence | ConvertTo-Json -Depth 10), [System.Text.UTF8Encoding]::new($false))
                    [Console]::Error.WriteLine("Missing required tool: $tool for gate $($gate.id)")
                    exit 1
                }
                $resolvedTools[$tool] = @{ Path = $cmd.Source; Version = $cmd.Version.ToString() }
            }
        }
    }
    foreach ($file in $gate.requiredFiles) {
        $p = Get-SafeChildPath $expectedRoot $file "Required File"
        if (-not (Test-Path $p)) {
            $evidence.resolvedTools = $resolvedTools
            $evidence.overallResult = "FAIL"
            $evidence.firstFailure = $gate.id
            $preOutFile = Get-SafeChildPath $outDir "${Profile}-$($gate.id).out.txt" "Preflight Output"
            [IO.File]::WriteAllText($preOutFile, "Missing required file: $p for gate $($gate.id)", [System.Text.UTF8Encoding]::new($false))
            $preOutHash = (Get-FileHash -Path $preOutFile -Algorithm SHA256).Hash

            $evidence.gates += @{
                id = $gate.id
                status = $gate.status
                task = $gate.task
                startTime = (Get-Date).ToString("o")
                durationSeconds = 0
                exitCode = -1
                outputHash = $preOutHash
                error = "Missing required file: $p for gate $($gate.id)"
                command = $gate.command
                arguments = $gate.arguments
                encodedInvocation = ""
                outputPath = "${Profile}-$($gate.id).out.txt"
                activation = $gate.activation
                timeoutSeconds = $gate.timeoutSeconds
                processState = "preflight-failure"
            }
            $evidence.endTime = (Get-Date).ToString("o")
            [System.IO.File]::WriteAllText($evidenceFile, ($evidence | ConvertTo-Json -Depth 10), [System.Text.UTF8Encoding]::new($false))
            [Console]::Error.WriteLine("Missing required file: $p for gate $($gate.id)")
            exit 1
        }
    }
}
$evidence.resolvedTools = $resolvedTools

function Escape-Argument($arg) {
    if ([string]::IsNullOrEmpty($arg)) { return '""' }
    if ($arg -match "[\s`"]") {
        # Escape backslashes preceding quotes
        $escaped = [regex]::Replace($arg, '(\\+)(?=")', '$1$1')
        $escaped = $escaped -replace '"', '\"'
        # Escape trailing backslashes
        $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
        return "`"$escaped`""
    }
    return $arg
}

function Run-Gate {
    param([object]$Gate)

    $gateEv = @{
        id = $Gate.id
        status = $Gate.status
        task = $Gate.task
        startTime = (Get-Date).ToString("o")
        durationSeconds = 0
        exitCode = $null
        outputHash = $null
        error = $null
        command = $Gate.command
        arguments = @()
        encodedInvocation = ""
        outputPath = "${Profile}-$($Gate.id).out.txt"
        activation = $Gate.activation
        timeoutSeconds = $Gate.timeoutSeconds
        expectedOutputChecks = @{}
        processState = "completed"
    }

    Write-Host "`n=== Running Gate: $($Gate.id) ($($Gate.task)) ===" -ForegroundColor Cyan
    Write-Host "Profile: $Profile | Status: $($Gate.status) | Timeout: $($Gate.timeoutSeconds)s"

    $outFile = Get-SafeChildPath $outDir $gateEv.outputPath "Gate Output"

    if ($Gate.status -eq 'NOT_APPLICABLE') {
        Write-Host "Gate is NOT_APPLICABLE ($($Gate.reason)). Skipping." -ForegroundColor Yellow
        $gateEv.exitCode = 0
        $gateEv.durationSeconds = 0
        [IO.File]::WriteAllText($outFile, "Gate is NOT_APPLICABLE ($($Gate.reason))", [System.Text.UTF8Encoding]::new($false))
        $gateEv.outputHash = (Get-FileHash -Path $outFile -Algorithm SHA256).Hash
        return $gateEv
    }

    if ($Gate.status -eq 'NOT_IMPLEMENTED') {
        Write-Host "Gate is NOT_IMPLEMENTED. Failing fast." -ForegroundColor Red
        $gateEv.exitCode = 1
        $gateEv.error = "Gate is NOT_IMPLEMENTED ($($Gate.activationCondition))"
        $gateEv.durationSeconds = 0
        [IO.File]::WriteAllText($outFile, $gateEv.error, [System.Text.UTF8Encoding]::new($false))
        $gateEv.outputHash = (Get-FileHash -Path $outFile -Algorithm SHA256).Hash
        return $gateEv
    }

    $sw = [Diagnostics.Stopwatch]::StartNew()

    $relEvidenceDir = $outDir.Substring($expectedRoot.Length).TrimStart('\', '/').Replace('\', '/')
    $argsArr = @()
    if ($Gate.arguments) {
        foreach ($a in $Gate.arguments) {
            $expandedA = $a -replace '\{EvidenceDir\}', $relEvidenceDir -replace '\{EvidenceRoot\}', $relEvidenceDir -replace '\{RunId\}', $runId
            $gateEv.arguments += $expandedA
            $argsArr += (Escape-Argument $expandedA)
        }
    }
    $encodedArgs = $argsArr -join ' '
    $gateEv.encodedInvocation = $encodedArgs

    $gateRunGuid = [guid]::NewGuid().ToString()
    $outTemp = Get-SafeChildPath $outDir "$($Gate.id)-${gateRunGuid}.tmp.out" "Temp Out"
    $errTemp = Get-SafeChildPath $outDir "$($Gate.id)-${gateRunGuid}.tmp.err" "Temp Err"
    $proc = $null

    try {
        $execFile = $Gate.command
        if ($resolvedTools.ContainsKey($Gate.command)) {
            $execFile = $resolvedTools[$Gate.command].Path
        }

        $isWin = $IsWindows -or ($env:OS -eq "Windows_NT")
        $startParams = @{
            FilePath = $execFile
            ArgumentList = $encodedArgs
            PassThru = $true
            RedirectStandardOutput = $outTemp
            RedirectStandardError = $errTemp
        }
        if ($isWin) {
            $startParams["WindowStyle"] = "Hidden"
        }
        $proc = Start-Process @startParams

        try {
            $proc | Wait-Process -Timeout $Gate.timeoutSeconds -ErrorAction Stop
            $gateEv.exitCode = $proc.ExitCode
        } catch {
            Write-Host "Gate timed out!" -ForegroundColor Red
            $gateEv.processState = "timeout"
            $gateEv.exitCode = -1
            $gateEv.error = "TIMEOUT"
            if ($null -ne $proc -and -not $proc.HasExited) {
                if ($isWin) {
                    try {
                        $killProc = Start-Process -FilePath "taskkill.exe" -ArgumentList "/T", "/F", "/PID", "$($proc.Id)" -WindowStyle Hidden -PassThru -ErrorAction SilentlyContinue
                        if ($null -ne $killProc) {
                            try { [void]$killProc.WaitForExit(5000) } catch {}
                            $killProc.Dispose()
                        }
                    } catch {}
                }
                $proc | Stop-Process -Force -ErrorAction SilentlyContinue
            }
            # Wait for the killed process to fully release file handles
            try { [void]$proc.WaitForExit(5000) } catch {}
            Start-Sleep -Milliseconds 200
        }

        # Read output files with retry to handle file-lock release delay
        $stdoutStr = ""
        $stderrStr = ""
        for ($retry = 0; $retry -lt 5; $retry++) {
            try {
                if (Test-Path $outTemp) {
                    $outBytes = [IO.File]::ReadAllBytes($outTemp)
                    $utf8Candidate = [System.Text.Encoding]::UTF8.GetString($outBytes)
                    if ($utf8Candidate.Contains([char]0xFFFD)) {
                        $stdoutStr = [System.Text.Encoding]::Default.GetString($outBytes)
                    } else {
                        $stdoutStr = $utf8Candidate
                    }
                }
                if (Test-Path $errTemp) {
                    $errBytes = [IO.File]::ReadAllBytes($errTemp)
                    $utf8Candidate = [System.Text.Encoding]::UTF8.GetString($errBytes)
                    if ($utf8Candidate.Contains([char]0xFFFD)) {
                        $stderrStr = [System.Text.Encoding]::Default.GetString($errBytes)
                    } else {
                        $stderrStr = $utf8Candidate
                    }
                }
                break
            } catch {
                if ($retry -eq 4) {
                    Write-Host "Warning: Could not read process output files after retries" -ForegroundColor Yellow
                }
                Start-Sleep -Milliseconds 200
            }
        }

        $content = "--- STDOUT ---`n" + $stdoutStr + "`n--- STDERR ---`n" + $stderrStr
        $content = $content -replace "SECRET_KEY=\w+", "SECRET_KEY=***REDACTED***"
        if ($env:POSTGRES_PASSWORD) {
            $content = $content -replace [regex]::Escape($env:POSTGRES_PASSWORD), "***REDACTED***"
        }
        $cleanLines = @()
        foreach ($line in ($content -split "`r?`n")) {
            $cleanLines += $line.TrimEnd()
        }
        $content = $cleanLines -join "`r`n"

        [IO.File]::WriteAllText($outFile, $content, [System.Text.UTF8Encoding]::new($false))
        $gateEv.outputHash = (Get-FileHash -Path $outFile -Algorithm SHA256).Hash

        foreach ($out in $Gate.expectedOutputs) {
            $expandedOut = $out -replace '\{EvidenceDir\}', $relEvidenceDir -replace '\{EvidenceRoot\}', $relEvidenceDir
            $expectedOutPath = Get-SafeChildPath $expectedRoot $expandedOut "Expected Output"
            if (Test-Path $expectedOutPath) {
                $gateEv.expectedOutputChecks[$expandedOut] = @{ exists = $true; hash = (Get-FileHash -Path $expectedOutPath -Algorithm SHA256).Hash }
            } else {
                $gateEv.expectedOutputChecks[$expandedOut] = @{ exists = $false }
            }
        }
    } catch {
        Write-Host "Failed to start process: $_" -ForegroundColor Red
        $gateEv.exitCode = -1
        $gateEv.error = $_.Exception.Message
        [IO.File]::WriteAllText($outFile, $gateEv.error, [System.Text.UTF8Encoding]::new($false))
        $gateEv.outputHash = (Get-FileHash -Path $outFile -Algorithm SHA256).Hash
    } finally {
        $sw.Stop()
        $gateEv.durationSeconds = [Math]::Round($sw.Elapsed.TotalSeconds, 2)
        if ($proc -ne $null) { $proc.Dispose() }
        if (Test-Path $outTemp) { Remove-Item $outTemp -Force -ErrorAction SilentlyContinue }
        if (Test-Path $errTemp) { Remove-Item $errTemp -Force -ErrorAction SilentlyContinue }
    }

    if ($gateEv.exitCode -ne 0) {
        Write-Host "Gate failed with exit code $($gateEv.exitCode)" -ForegroundColor Red
    } else {
        Write-Host "Gate succeeded." -ForegroundColor Green
    }

    return $gateEv
}

$failed = $false
$totalDurationSw = [Diagnostics.Stopwatch]::StartNew()

foreach ($gate in $selectedGates) {
    $res = Run-Gate -Gate $gate
    $evidence.gates += $res

    if ($res.exitCode -ne 0) {
        $evidence.firstFailure = $gate.id
        $evidence.overallResult = "FAIL"
        $failed = $true
        break
    }
}
$totalDurationSw.Stop()
$evidence.totalDurationSeconds = [Math]::Round($totalDurationSw.Elapsed.TotalSeconds, 2)

$postLocks = Get-LockSnapshot
foreach ($lock in $expectedLocks) {
    $pre = $preLocks[$lock]
    $post = $postLocks[$lock]
    if ($pre.Hash -ne $post.Hash -or $pre.Length -ne $post.Length) {
        throw "Lock file mutated during run: $lock"
    }
}
$evidence.postLocks = $postLocks

$evidence.endTime = (Get-Date).ToString("o")
[System.IO.File]::WriteAllText($evidenceFile, ($evidence | ConvertTo-Json -Depth 10), [System.Text.UTF8Encoding]::new($false))

if ($disposablePostgresSecret) {
    Remove-Item env:POSTGRES_PASSWORD -ErrorAction SilentlyContinue
}

if ($failed) { exit 1 }
exit 0
