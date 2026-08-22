# run-r3-verification.ps1 - BR001-R3 Deterministic Verification Runner
$ErrorActionPreference = 'Stop'

if (-not (Test-Path ".git")) {
    throw "run-r3-verification.ps1 must be executed from the repository root"
}

$repoRoot = (Get-Item .).FullName
$runDir = "artifacts\task-runs\open_source-cab.7"
$runDirFull = Join-Path $repoRoot $runDir
$qgDirFull = Join-Path $repoRoot "artifacts\quality-gate"

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

[void](Assert-SafePathChain $repoRoot $repoRoot)
[void](Assert-SafePathChain $runDirFull $repoRoot)
if (-not (Test-Path $qgDirFull)) {
    New-Item -ItemType Directory -Path $qgDirFull -Force | Out-Null
}
[void](Assert-SafePathChain $qgDirFull $repoRoot)

$script:activeProcs = @()
$script:runTempDir = $null

function Escape-Argument($arg) {
    if ([string]::IsNullOrEmpty($arg)) { return '""' }
    if ($arg -match "[\s`"]") {
        $escaped = [regex]::Replace($arg, '(\\+)(?=")', '$1$1')
        $escaped = $escaped -replace '"', '\"'
        $escaped = [regex]::Replace($escaped, '(\\+)$', '$1$1')
        return "`"$escaped`""
    }
    return $arg
}

function Invoke-Logged {
    param(
        [string]$Name,
        [string]$CommandName,
        [string[]]$ArgumentList,
        [int]$ExpectedExitCode,
        [int]$TimeoutSeconds = 120
    )

    Write-Host "Running: $Name" -ForegroundColor Cyan
    $sw = [Diagnostics.Stopwatch]::StartNew()
    $outPath = Join-Path $runDir "$Name.txt"
    $outPathFull = Join-Path $repoRoot $outPath

    $stepGuid = [guid]::NewGuid().ToString()
    $outTemp = Join-Path $script:runTempDir "${Name}-${stepGuid}.tmp.out"
    $errTemp = Join-Path $script:runTempDir "${Name}-${stepGuid}.tmp.err"
    $proc = $null

    try {
        $encodedArgs = ($ArgumentList | ForEach-Object { Escape-Argument $_ }) -join ' '

        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = $CommandName
        $psi.WorkingDirectory = $repoRoot
        $psi.Arguments = $encodedArgs
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.UseShellExecute = $false
        $psi.CreateNoWindow = $true

        $proc = New-Object System.Diagnostics.Process
        $proc.StartInfo = $psi
        $proc.Start() | Out-Null
        $script:activeProcs += $proc

        $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
        $stderrTask = $proc.StandardError.ReadToEndAsync()

        $completed = $proc.WaitForExit($TimeoutSeconds * 1000)
        $sw.Stop()

        if (-not $completed) {
            try {
                $kill = Start-Process -FilePath "taskkill.exe" -ArgumentList "/T", "/F", "/PID", "$($proc.Id)" -WindowStyle Hidden -PassThru -ErrorAction SilentlyContinue
                if ($null -ne $kill) {
                    try { [void]$kill.WaitForExit(5000) } catch {}
                    $kill.Dispose()
                }
                $proc.Kill()
            } catch {}
            throw "Command '$Name' timed out after ${TimeoutSeconds}s"
        }

        [void]$stdoutTask.Wait(5000)
        [void]$stderrTask.Wait(5000)

        $stdout = $stdoutTask.Result
        $stderr = $stderrTask.Result
        $exitCode = $proc.ExitCode

        $content = "COMMAND: $CommandName $encodedArgs`nEXIT_CODE: $exitCode`nDURATION_MS: $($sw.ElapsedMilliseconds)`nSTDOUT:`n$stdout`nSTDERR:`n$stderr"
        [IO.File]::WriteAllText($outPathFull, $content, [System.Text.Encoding]::UTF8)

        if ($exitCode -ne $ExpectedExitCode) {
            Write-Host "FATAL: $Name exited with $exitCode but expected $ExpectedExitCode" -ForegroundColor Red
            throw "Command '$Name' exited with $exitCode but expected $ExpectedExitCode"
        }

        return @{ ExitCode = $exitCode; Duration = $sw.ElapsedMilliseconds; LogPath = $outPath; Stdout = $stdout; Stderr = $stderr }
    } finally {
        if ($null -ne $proc) {
            try { $proc.Dispose() } catch {}
        }
        if (Test-Path $outTemp) { Remove-Item -Path $outTemp -Force -ErrorAction SilentlyContinue }
        if (Test-Path $errTemp) { Remove-Item -Path $errTemp -Force -ErrorAction SilentlyContinue }
    }
}

try {
    Write-Host "Initializing Verification Run Lifecycle..." -ForegroundColor Cyan

    $runId = [guid]::NewGuid().ToString()
    $script:runTempDir = Join-Path $qgDirFull "run-temp-$runId"
    [void](Assert-SafePathChain $script:runTempDir $repoRoot)
    New-Item -ItemType Directory -Path $script:runTempDir -Force | Out-Null

    $frozenRunDirRel = "artifacts\quality-gate\frozen-r3-run"
    $frozenRunDirFull = Join-Path $repoRoot $frozenRunDirRel
    [void](Assert-SafePathChain $frozenRunDirFull $repoRoot)
    if (Test-Path $frozenRunDirFull) {
        Remove-Item -Path $frozenRunDirFull -Recurse -Force -ErrorAction Stop
    }
    New-Item -ItemType Directory -Path $frozenRunDirFull -Force | Out-Null

    $foundEvRel = "$frozenRunDirRel\evidence-Foundation.json"
    $runtimeEvRel = "$frozenRunDirRel\evidence-Runtime.json"
    $fullEvRel = "$frozenRunDirRel\evidence-Full.json"

    # 1. PowerShell Parser Preflight
    $parserScript = '$ErrorActionPreference=''Stop''; foreach ($f in @(''scripts\check.ps1'', ''scripts\verify-check-contract.ps1'')) { $parseErr=$null; [System.Management.Automation.Language.Parser]::ParseFile($f, [ref]$null, [ref]$parseErr); if ($parseErr) { throw $parseErr } }'
    [void](Invoke-Logged -Name "parser" -CommandName "powershell.exe" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $parserScript) -ExpectedExitCode 0 -TimeoutSeconds 30)

    # 2. Comprehensive Contract Verifier
    [void](Invoke-Logged -Name "verifier" -CommandName "powershell.exe" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts\verify-check-contract.ps1") -ExpectedExitCode 0 -TimeoutSeconds 180)

    # 3. Real Foundation Profile
    [void](Invoke-Logged -Name "foundation" -CommandName "powershell.exe" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts\check.ps1", "-Profile", "Foundation", "-EvidencePath", $foundEvRel) -ExpectedExitCode 0 -TimeoutSeconds 60)

    # 4. Real Runtime Profile
    [void](Invoke-Logged -Name "runtime" -CommandName "powershell.exe" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts\check.ps1", "-Profile", "Runtime", "-EvidencePath", $runtimeEvRel) -ExpectedExitCode 1 -TimeoutSeconds 60)

    # 5. Real Full Profile (default invocation without -Profile)
    [void](Invoke-Logged -Name "full" -CommandName "powershell.exe" -ArgumentList @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts\check.ps1", "-EvidencePath", $fullEvRel) -ExpectedExitCode 1 -TimeoutSeconds 60)

    # 6. Locked Restore
    [void](Invoke-Logged -Name "restore" -CommandName "dotnet.exe" -ArgumentList @("restore", "DXOS.slnx", "--locked-mode") -ExpectedExitCode 0 -TimeoutSeconds 60)

    # 7. Whitespace Formatting Verification
    [void](Invoke-Logged -Name "format" -CommandName "dotnet.exe" -ArgumentList @("format", "whitespace", "DXOS.slnx", "--verify-no-changes", "--no-restore") -ExpectedExitCode 0 -TimeoutSeconds 60)

    # 8. Release Build Verification
    [void](Invoke-Logged -Name "build" -CommandName "dotnet.exe" -ArgumentList @("build", "DXOS.slnx", "-c", "Release", "--no-restore", "-warnaserror") -ExpectedExitCode 0 -TimeoutSeconds 60)

    # 9. OpenSpec Validation
    [void](Invoke-Logged -Name "openspec" -CommandName "openspec.cmd" -ArgumentList @("validate", "bootstrap-remediation-001", "--type", "change", "--strict", "--no-interactive") -ExpectedExitCode 0 -TimeoutSeconds 30)

    # 10. Beads Show & Semantic Verification
    $bdShowRes = Invoke-Logged -Name "bd-show" -CommandName "bd.cmd" -ArgumentList @("show", "open_source-cab.7", "--json") -ExpectedExitCode 0 -TimeoutSeconds 30
    $bdShowJson = $bdShowRes.Stdout | ConvertFrom-Json
    if ($bdShowJson[0].status -ne "in_progress" -or $bdShowJson[0].id -ne "open_source-cab.7") {
        throw "Semantic violation: Beads issue open_source-cab.7 status is '$($bdShowJson[0].status)' (expected 'in_progress')"
    }

    # 11. Beads Cycles & Semantic Verification
    $bdCyclesRes = Invoke-Logged -Name "bd-cycles" -CommandName "bd.cmd" -ArgumentList @("dep", "cycles") -ExpectedExitCode 0 -TimeoutSeconds 30
    if (-not $bdCyclesRes.Stdout.Contains("No dependency cycles detected")) {
        throw "Semantic violation: Beads cycles detected"
    }

    # 12. Git Diff Check
    $gitDiffRes = Invoke-Logged -Name "git-diff" -CommandName "git.exe" -ArgumentList @("diff", "--check") -ExpectedExitCode 0 -TimeoutSeconds 30
    if (-not [string]::IsNullOrWhiteSpace($gitDiffRes.Stdout.Trim())) {
        throw "Semantic violation: git diff --check returned whitespace errors: $($gitDiffRes.Stdout)"
    }

    # 13. Git Status Short & Exact File-Level Scope Validation (--untracked-files=all)
    $gitStatusRes = Invoke-Logged -Name "git-status" -CommandName "git.exe" -ArgumentList @("status", "--short", "--untracked-files=all") -ExpectedExitCode 0 -TimeoutSeconds 30
    $actualStatusLines = @($gitStatusRes.Stdout -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    $expectedStatusLines = @(
        " M .gitignore",
        " M scripts/check.ps1",
        " M src/DXOS.Api/Program.cs",
        " M src/DXOS.AppHost/Program.cs",
        " M src/DXOS.Application/Class1.cs",
        " M src/DXOS.Domain/Class1.cs",
        " M src/DXOS.Infrastructure/Class1.cs",
        " M src/DXOS.Workflows/Class1.cs",
        "?? artifacts/task-runs/open_source-cab.7/bd-cycles.txt",
        "?? artifacts/task-runs/open_source-cab.7/bd-show.txt",
        "?? artifacts/task-runs/open_source-cab.7/build.txt",
        "?? artifacts/task-runs/open_source-cab.7/format.txt",
        "?? artifacts/task-runs/open_source-cab.7/foundation.txt",
        "?? artifacts/task-runs/open_source-cab.7/full.txt",
        "?? artifacts/task-runs/open_source-cab.7/git-diff.txt",
        "?? artifacts/task-runs/open_source-cab.7/git-status.txt",
        "?? artifacts/task-runs/open_source-cab.7/implementation-report.md",
        "?? artifacts/task-runs/open_source-cab.7/openspec.txt",
        "?? artifacts/task-runs/open_source-cab.7/parser.txt",
        "?? artifacts/task-runs/open_source-cab.7/prompt.md",
        "?? artifacts/task-runs/open_source-cab.7/reports.sha256",
        "?? artifacts/task-runs/open_source-cab.7/restore.txt",
        "?? artifacts/task-runs/open_source-cab.7/review.md",
        "?? artifacts/task-runs/open_source-cab.7/run-r3-verification.ps1",
        "?? artifacts/task-runs/open_source-cab.7/runtime.txt",
        "?? artifacts/task-runs/open_source-cab.7/verification-output.sha256",
        "?? artifacts/task-runs/open_source-cab.7/verification.md",
        "?? artifacts/task-runs/open_source-cab.7/verifier.txt",
        "?? scripts/check-contract.json",
        "?? scripts/verify-check-contract.ps1"
    )

    if ($actualStatusLines.Length -ne $expectedStatusLines.Length) {
        throw "Semantic violation: git status line count mismatch (expected $($expectedStatusLines.Length), got $($actualStatusLines.Length))`nActual:`n$($actualStatusLines -join "`n")"
    }
    for ($i = 0; $i -lt $expectedStatusLines.Length; $i++) {
        $actualNorm = $actualStatusLines[$i].Replace('\', '/')
        $expectedNorm = $expectedStatusLines[$i].Replace('\', '/')
        if ($actualNorm -ne $expectedNorm) {
            throw "Semantic violation: git status line [$i] mismatch (expected '$expectedNorm', got '$actualNorm')"
        }
    }

    # Semantic Profile Evidence Assertions for Run-Owned Evidence Files
    $foundEvidenceFull = Join-Path $repoRoot $foundEvRel
    $runtimeEvidenceFull = Join-Path $repoRoot $runtimeEvRel
    $fullEvidenceFull = Join-Path $repoRoot $fullEvRel

    if (-not (Test-Path $foundEvidenceFull) -or -not (Test-Path $runtimeEvidenceFull) -or -not (Test-Path $fullEvidenceFull)) {
        throw "Semantic violation: Missing one or more run-owned profile evidence files"
    }

    $expectedSixGates = @(
        @{ id = "foundation-restore"; exitCode = 0 },
        @{ id = "foundation-format"; exitCode = 0 },
        @{ id = "foundation-build"; exitCode = 0 },
        @{ id = "foundation-openspec"; exitCode = 0 },
        @{ id = "foundation-hygiene"; exitCode = 0 },
        @{ id = "runtime-unit-tests"; exitCode = 1 }
    )

    # Foundation Profile Exact Assertions (5 gates in order)
    $foundData = Get-Content $foundEvidenceFull -Raw | ConvertFrom-Json
    if ($foundData.profile -ne "Foundation" -or $foundData.overallResult -ne "PASS" -or $null -ne $foundData.firstFailure) {
        throw "Semantic violation: Foundation evidence mismatch (overallResult='$($foundData.overallResult)', firstFailure='$($foundData.firstFailure)')"
    }
    if ($foundData.gates.Count -ne 5) {
        throw "Semantic violation: Foundation gates count mismatch (expected 5, got $($foundData.gates.Count))"
    }
    for ($i = 0; $i -lt 5; $i++) {
        if ($foundData.gates[$i].id -ne $expectedSixGates[$i].id -or $foundData.gates[$i].exitCode -ne 0) {
            throw "Semantic violation: Foundation gate [$i] mismatch ($($foundData.gates[$i].id), exit=$($foundData.gates[$i].exitCode))"
        }
    }

    # Runtime Profile Exact Assertions (exact 6 gates in order)
    $runtimeData = Get-Content $runtimeEvidenceFull -Raw | ConvertFrom-Json
    if ($runtimeData.profile -ne "Runtime" -or $runtimeData.overallResult -ne "FAIL" -or $runtimeData.firstFailure -ne "runtime-unit-tests") {
        throw "Semantic violation: Runtime evidence failure mismatch (overallResult='$($runtimeData.overallResult)', firstFailure='$($runtimeData.firstFailure)')"
    }
    if ($runtimeData.gates.Count -ne 6) {
        throw "Semantic violation: Runtime gate count mismatch (expected 6, got $($runtimeData.gates.Count))"
    }
    for ($i = 0; $i -lt 6; $i++) {
        if ($runtimeData.gates[$i].id -ne $expectedSixGates[$i].id -or $runtimeData.gates[$i].exitCode -ne $expectedSixGates[$i].exitCode) {
            throw "Semantic violation: Runtime gate [$i] mismatch ($($runtimeData.gates[$i].id), exit=$($runtimeData.gates[$i].exitCode))"
        }
    }

    # Full Profile Exact Assertions (default invocation, exact 6 gates in order)
    $fullData = Get-Content $fullEvidenceFull -Raw | ConvertFrom-Json
    if ($fullData.profile -ne "Full" -or $fullData.overallResult -ne "FAIL" -or $fullData.firstFailure -ne "runtime-unit-tests") {
        throw "Semantic violation: Full evidence failure mismatch (profile='$($fullData.profile)', overallResult='$($fullData.overallResult)', firstFailure='$($fullData.firstFailure)')"
    }
    if ($fullData.gates.Count -ne 6) {
        throw "Semantic violation: Full gate count mismatch (expected 6, got $($fullData.gates.Count))"
    }
    for ($i = 0; $i -lt 6; $i++) {
        if ($fullData.gates[$i].id -ne $expectedSixGates[$i].id -or $fullData.gates[$i].exitCode -ne $expectedSixGates[$i].exitCode) {
            throw "Semantic violation: Full gate [$i] mismatch ($($fullData.gates[$i].id), exit=$($fullData.gates[$i].exitCode))"
        }
    }

    # Mechanical verification of every gate output file and outputHash
    $retainedOutputFiles = @()
    foreach ($profData in @($foundData, $runtimeData, $fullData)) {
        foreach ($g in $profData.gates) {
            $gateOutRel = "$frozenRunDirRel\$($g.outputPath)"
            $gateOutFull = Join-Path $repoRoot $gateOutRel
            if (-not (Test-Path $gateOutFull)) {
                throw "Semantic violation: Missing referenced gate output file '$gateOutRel'"
            }
            $actualHash = (Get-FileHash -Path $gateOutFull -Algorithm SHA256).Hash
            if ($g.outputHash -ne $actualHash) {
                throw "Semantic violation: Output hash mismatch on '$gateOutRel' (evidence: $($g.outputHash), disk: $actualHash)"
            }
            if ($retainedOutputFiles -notcontains $gateOutRel) {
                $retainedOutputFiles += $gateOutRel
            }
        }
    }

    # Generate checksum sidecar containing scripts, transcripts, production files, 9 locks, 3 JSON evidence files, and all 17 referenced gate outputs
    $targetFiles = @(
        "scripts\check.ps1",
        "scripts\verify-check-contract.ps1",
        "scripts\check-contract.json",
        "artifacts\task-runs\open_source-cab.7\run-r3-verification.ps1",
        "artifacts\task-runs\open_source-cab.7\parser.txt",
        "artifacts\task-runs\open_source-cab.7\verifier.txt",
        "artifacts\task-runs\open_source-cab.7\foundation.txt",
        "artifacts\task-runs\open_source-cab.7\runtime.txt",
        "artifacts\task-runs\open_source-cab.7\full.txt",
        "artifacts\task-runs\open_source-cab.7\restore.txt",
        "artifacts\task-runs\open_source-cab.7\format.txt",
        "artifacts\task-runs\open_source-cab.7\build.txt",
        "artifacts\task-runs\open_source-cab.7\openspec.txt",
        "artifacts\task-runs\open_source-cab.7\bd-show.txt",
        "artifacts\task-runs\open_source-cab.7\bd-cycles.txt",
        "artifacts\task-runs\open_source-cab.7\git-diff.txt",
        "artifacts\task-runs\open_source-cab.7\git-status.txt",
        "src\DXOS.Api\Program.cs",
        "src\DXOS.AppHost\Program.cs",
        "src\DXOS.Application\Class1.cs",
        "src\DXOS.Domain\Class1.cs",
        "src\DXOS.Infrastructure\Class1.cs",
        "src\DXOS.Workflows\Class1.cs",
        "src\DXOS.Api\packages.lock.json",
        "src\DXOS.AppHost\packages.lock.json",
        "src\DXOS.Application\packages.lock.json",
        "src\DXOS.Domain\packages.lock.json",
        "src\DXOS.Infrastructure\packages.lock.json",
        "src\DXOS.Workflows\packages.lock.json",
        "tests\DXOS.Architecture.Tests\packages.lock.json",
        "tests\DXOS.Integration.Tests\packages.lock.json",
        "tests\DXOS.Unit.Tests\packages.lock.json",
        $foundEvRel,
        $fullEvRel,
        $runtimeEvRel
    )

    foreach ($outRel in $retainedOutputFiles) {
        $targetFiles += $outRel
    }

    $checksumLines = @()
    foreach ($f in $targetFiles) {
        $fullPath = Join-Path $repoRoot $f
        if (-not (Test-Path $fullPath)) {
            throw "Missing file for sidecar hashing: $f"
        }
        $hash = (Get-FileHash -Path $fullPath -Algorithm SHA256).Hash.ToLower()
        $normPath = $f.Replace('\', '/')
        $checksumLines += "$hash *$normPath"
    }

    $sidecarPath = Join-Path $runDirFull "verification-output.sha256"
    [IO.File]::WriteAllLines($sidecarPath, $checksumLines, [System.Text.Encoding]::UTF8)
    Write-Host "Sidecar generated at $sidecarPath with $($checksumLines.Count) entries" -ForegroundColor Green
} finally {
    foreach ($p in $script:activeProcs) {
        if ($null -ne $p) {
            try {
                if (-not $p.HasExited) {
                    $kill = Start-Process -FilePath "taskkill.exe" -ArgumentList "/T", "/F", "/PID", "$($p.Id)" -WindowStyle Hidden -PassThru -ErrorAction SilentlyContinue
                    if ($null -ne $kill) {
                        try { [void]$kill.WaitForExit(5000) } catch {}
                        $kill.Dispose()
                    }
                    $p.Kill()
                }
            } catch {}
            try { $p.Dispose() } catch {}
        }
    }
    if ($null -ne $script:runTempDir -and (Test-Path $script:runTempDir)) {
        [void](Assert-SafePathChain $script:runTempDir $repoRoot)
        Remove-Item -Path $script:runTempDir -Recurse -Force -ErrorAction Stop
        if (Test-Path $script:runTempDir) {
            throw "Fatal cleanup failure: Temporary directory '$script:runTempDir' was not removed"
        }
    }
}
exit 0
