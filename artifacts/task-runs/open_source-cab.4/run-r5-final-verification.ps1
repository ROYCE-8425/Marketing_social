[CmdletBinding()]
param(
    [string]$RunGuid = ([Guid]::NewGuid().ToString('N').Substring(0, 12)),
    [int]$TimeoutSeconds = 600
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

# 1. Validate RunGuid parameter format strictly (8-32 hex characters)
if ($RunGuid -notmatch '^[a-fA-F0-9]{8,32}$') {
    throw "Parameter validation failed: RunGuid must be an 8 to 32 character hexadecimal string, got '$RunGuid'"
}

$wrapperStartTime = [DateTimeOffset]::UtcNow
$wrapperPid = $PID

$evidenceDir = $PSScriptRoot
$gitRoot = (Get-Item $evidenceDir).Parent.Parent.Parent.FullName
$currentDir = (Get-Item .).FullName
if ($currentDir -ne $gitRoot) {
    Set-Location $gitRoot
}

. (Join-Path $evidenceDir "finalizer-publish-helper.ps1")

# 2. Validate ancestor chain of evidence directory and runs root
Assert-SafePathChain $evidenceDir $gitRoot

$runsBaseDir = Join-Path $evidenceDir "runs"
if (-not (Test-Path $runsBaseDir)) {
    New-Item -ItemType Directory -Path $runsBaseDir | Out-Null
}
Assert-SafePathChain $runsBaseDir $evidenceDir

$runDir = Join-Path $runsBaseDir $RunGuid
Assert-SafePathChain $runDir $runsBaseDir

# Reject pre-existing run directory to ensure single-use run GUID
if (Test-Path $runDir) {
    throw "Run isolation violation: Run directory '$runDir' already exists for RunGuid '$RunGuid'. Run IDs must be strictly unique."
}

# 3. Exclusive Concurrency Run Lock
$lockFilePath = Join-Path $evidenceDir ".run.lock"
$lockFileStream = $null
try {
    $lockFileStream = [System.IO.File]::Open($lockFilePath, [System.IO.FileMode]::OpenOrCreate, [System.IO.FileAccess]::ReadWrite, [System.IO.FileShare]::None)
} catch {
    throw "Concurrency violation: another verification runner is currently running or locked for this task!"
}

$childPid = $null
$childExitCode = $null
$childStartTime = $null
$childEndTime = $null
$childDurationSeconds = 0
$childCommand = "powershell.exe -NoProfile -ExecutionPolicy Bypass -File artifacts/task-runs/open_source-cab.4/run-r5-verification.ps1 -RunGuid $RunGuid"
$childStdout = ""
$childStderr = ""

try {
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "STARTING BR001-R5 EXTERNAL BOUNDED FINALIZER & VERIFICATION ORCHESTRATOR" -ForegroundColor Cyan
    Write-Host "Wrapper PID: $wrapperPid | Run GUID: $RunGuid | Timeout: ${TimeoutSeconds}s" -ForegroundColor Cyan
    Write-Host "Evidence Dir: $runDir" -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor Cyan

    # 4. Launch Child Verification Runner exactly once
    $childPsi = New-Object System.Diagnostics.ProcessStartInfo
    $childPsi.FileName = "powershell.exe"
    $childPsi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File artifacts/task-runs/open_source-cab.4/run-r5-verification.ps1 -RunGuid $RunGuid"
    $childPsi.WorkingDirectory = $gitRoot
    $childPsi.RedirectStandardOutput = $true
    $childPsi.RedirectStandardError = $true
    $childPsi.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $childPsi.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
    $childPsi.UseShellExecute = $false
    $childPsi.CreateNoWindow = $true

    $childStartTime = [DateTimeOffset]::UtcNow
    $childProc = [System.Diagnostics.Process]::Start($childPsi)
    $childPid = $childProc.Id

    Write-Host "[WRAPPER] Child verification runner launched with PID: $childPid" -ForegroundColor Cyan

    $stdoutTask = $childProc.StandardOutput.ReadToEndAsync()
    $stderrTask = $childProc.StandardError.ReadToEndAsync()

    $timeoutMs = $TimeoutSeconds * 1000
    if (-not $childProc.WaitForExit($timeoutMs)) {
        try {
            & taskkill /PID $childProc.Id /T /F 2>&1 | Out-Null
        } catch {
            $childProc.Kill()
        }
        throw "Child verification runner timed out after ${TimeoutSeconds}s (PID: $childPid)"
    }

    $childEndTime = [DateTimeOffset]::UtcNow
    $childDurationSeconds = ($childEndTime - $childStartTime).TotalSeconds
    $childExitCode = $childProc.ExitCode

    $childStdout = $stdoutTask.Result
    $childStderr = $stderrTask.Result

    Write-Host $childStdout

    if ($childExitCode -ne 0) {
        Write-Host $childStderr -ForegroundColor Red
        throw "Child verification runner failed with exit code $childExitCode"
    }

    Write-Host "[WRAPPER] Child runner exited successfully with code 0 in $([Math]::Round($childDurationSeconds, 3))s" -ForegroundColor Green

    # 5. Retain Captured Child Streams
    $childStdoutPath = Join-Path $runDir "child-stdout.log"
    $childStderrPath = Join-Path $runDir "child-stderr.log"
    Write-CleanFile $childStdoutPath $childStdout
    Write-CleanFile $childStderrPath $childStderr

    $childStdoutHash = (Get-FileHash -Path $childStdoutPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $childStderrHash = (Get-FileHash -Path $childStderrPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $childStdoutBytes = (Get-Item $childStdoutPath).Length
    $childStderrBytes = (Get-Item $childStderrPath).Length

    Write-Host "[WRAPPER] Retained child streams: child-stdout.log ($childStdoutBytes bytes), child-stderr.log ($childStderrBytes bytes)" -ForegroundColor Green

    # 6. Write Pre-Finalization Child Execution Record (child-execution.log)
    $childExecLogPath = Join-Path $runDir "child-execution.log"
    $childExecContent = @"
================================================================================
BR001-R5 CHILD VERIFICATION RUNNER EXECUTION RECORD
================================================================================
Run GUID: $RunGuid
Evidence Directory: $runDir
Child PID: $childPid
Child Command: $childCommand
Child Start: $($childStartTime.ToString("o"))
Child End: $($childEndTime.ToString("o"))
Child Wall Duration: $([Math]::Round($childDurationSeconds, 3))s
Child Exit Code: $childExitCode
Child Result: PASS

RETAINED CHILD STREAMS:
1. child-stdout.log: $childStdoutBytes bytes (SHA256: $childStdoutHash)
2. child-stderr.log: $childStderrBytes bytes (SHA256: $childStderrHash)
================================================================================
STATUS: CHILD_EXECUTION_COMPLETED
================================================================================
"@
    Write-CleanFile $childExecLogPath $childExecContent

    # 7. Generate Complete Sidecars Covering All Completed Artifacts
    $verifOutSidecar = Join-Path $evidenceDir "verification-output.sha256"
    $reportsSidecar = Join-Path $evidenceDir "reports.sha256"

    $verifOutLines = @()
    $allRunFiles = Get-ChildItem -Path $runDir -File | Sort-Object Name
    foreach ($f in $allRunFiles) {
        $fHash = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $relPath = "artifacts/task-runs/open_source-cab.4/runs/$RunGuid/" + $f.Name
        $verifOutLines += "$fHash  $relPath"
    }

    $materialInputFiles = @(
        "global.json",
        "Directory.Build.props",
        "Directory.Packages.props",
        "DXOS.slnx",
        "docs/testing.md",
        "scripts/check.ps1",
        "scripts/check-contract.json",
        "scripts/verify-check-contract.ps1",
        "scripts/run-test-project.ps1",
        "scripts/smoke-runtime.ps1",
        "openspec/changes/bootstrap-remediation-001/tasks.md"
    )
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
    foreach ($lock in $expectedLocks) {
        $materialInputFiles += $lock
    }

    $gitLsSrc = (& git ls-files src tests)
    foreach ($line in ($gitLsSrc -split "`r?`n")) {
        $trimmed = $line.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed) -and ($trimmed -match '\.(cs|csproj)$')) {
            $candidatePath = Join-Path $gitRoot $trimmed
            if (Test-Path $candidatePath) {
                $materialInputFiles += $trimmed.Replace('\', '/')
            }
        }
    }

    $untrackedDeliverables = @(
        "tests/DXOS.Architecture.Tests/ArchitectureTests.cs",
        "tests/DXOS.Architecture.Tests/Fixtures/ViolatingDomainFixture.cs",
        "tests/DXOS.Architecture.Tests/Fixtures/ViolatingProjectReferenceFixture.cs",
        "tests/DXOS.Integration.Tests/ContainerTeardownFixtureTests.cs",
        "tests/DXOS.Integration.Tests/PostgresAndElsaIntegrationTests.cs",
        "tests/DXOS.Integration.Tests/Teardown/ContainerTeardownHelper.cs",
        "tests/DXOS.Unit.Tests/BootstrapDbContextFactoryTests.cs",
        "tests/DXOS.Unit.Tests/BootstrapDbContextModelTests.cs"
    )
    foreach ($ud in $untrackedDeliverables) {
        $candidatePath = Join-Path $gitRoot $ud
        if (Test-Path $candidatePath) {
            $materialInputFiles += $ud
        }
    }

    $uniqueMaterialFiles = $materialInputFiles | Select-Object -Unique | Sort-Object
    foreach ($mf in $uniqueMaterialFiles) {
        if ($mf -match '/(bin|obj)/') {
            throw "Sidecar security violation: generated intermediate '$mf' must not be in sidecar manifest"
        }
        $fullMfPath = Join-Path $gitRoot $mf
        if (Test-Path $fullMfPath) {
            $mfHash = (Get-FileHash -Path $fullMfPath -Algorithm SHA256).Hash.ToLowerInvariant()
            $verifOutLines += "$mfHash  $mf"
        } else {
            throw "Missing material file in sidecar: $mf"
        }
    }

    Write-CleanFile $verifOutSidecar (($verifOutLines | Sort-Object -Unique) -join "`r`n")

    $reportFiles = @(
        "prompt.md",
        "package-and-license-delta.md",
        "implementation-report.md",
        "verification.md",
        "run-r5-verification.ps1",
        "run-r5-final-verification.ps1",
        "finalizer-publish-helper.ps1",
        "test-finalizer-ordering-fixture.ps1",
        "finalizer-ordering-fixture.log",
        "verification-output.sha256"
    )
    $reportLines = @()
    foreach ($rf in $reportFiles) {
        $fullRfPath = Join-Path $evidenceDir $rf
        if (Test-Path $fullRfPath) {
            $rfHash = (Get-FileHash -Path $fullRfPath -Algorithm SHA256).Hash.ToLowerInvariant()
            $relRfPath = "artifacts/task-runs/open_source-cab.4/$rf"
            $reportLines += "$rfHash  $relRfPath"
        }
    }
    Write-CleanFile $reportsSidecar ($reportLines -join "`r`n")

    # 8. Post-Child Independent Validations
    Write-Host "`n[WRAPPER] Executing Post-Child Independent Validations..." -ForegroundColor Cyan

    # 8.1 Strict Text Hygiene Scan
    $allDeliverables = @(
        "global.json",
        "Directory.Build.props",
        "Directory.Packages.props",
        "DXOS.slnx",
        "docs/testing.md",
        "scripts/check.ps1",
        "scripts/check-contract.json",
        "scripts/verify-check-contract.ps1",
        "scripts/run-test-project.ps1",
        "scripts/smoke-runtime.ps1",
        "openspec/changes/bootstrap-remediation-001/tasks.md"
    )

    $filesToScan = @()
    foreach ($drp in $allDeliverables) {
        $fullPath = Join-Path $gitRoot $drp
        if (Test-Path $fullPath) { $filesToScan += Get-Item $fullPath }
    }
    $filesToScan += Get-ChildItem -Path (Join-Path $gitRoot "src") -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
    $filesToScan += Get-ChildItem -Path (Join-Path $gitRoot "tests") -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
    $filesToScan += Get-ChildItem -Path $evidenceDir -File | Where-Object { $_.Name -ne "review.md" -and $_.Name -ne ".run.lock" }
    $filesToScan += Get-ChildItem -Path $runDir -File

    $uniqueScanFiles = $filesToScan | Sort-Object -Property FullName -Unique
    $hygieneErrors = @()
    foreach ($f in $uniqueScanFiles) {
        $err = Test-StrictFileHygiene $f.FullName
        if ($null -ne $err) {
            $hygieneErrors += "$($f.FullName): $err"
        }
    }
    if ($hygieneErrors.Count -gt 0) {
        throw "Post-child text hygiene check failed:`n" + ($hygieneErrors -join "`n")
    }
    Write-Host "[PASS] Text hygiene verified: $($uniqueScanFiles.Count) files clean (0 violations)" -ForegroundColor Green

    # 8.2 Markdown Link Verification
    $verifMdPath = Join-Path $evidenceDir "verification.md"
    if (-not (Test-Path $verifMdPath)) {
        throw "Missing verification.md at: $verifMdPath"
    }
    $verifMdText = Get-Content $verifMdPath -Raw
    $linkMatches = [regex]::Matches($verifMdText, '\[([^\]]+)\]\(([^)]+)\)')
    $verifiedLinksCount = 0
    foreach ($match in $linkMatches) {
        $linkTarget = $match.Groups[2].Value
        $resolvedTarget = Join-Path $evidenceDir $linkTarget
        if (-not (Test-Path $resolvedTarget)) {
            throw "Post-child link verification failure: '$linkTarget' in verification.md does not resolve to an existing file at '$resolvedTarget'"
        }
        $verifiedLinksCount++
    }
    Write-Host "[PASS] Markdown links verified: $verifiedLinksCount links resolve to valid files on disk" -ForegroundColor Green

    # 8.3 Sidecar Checksum Validation
    $finalVerifOutLines = [System.IO.File]::ReadAllLines($verifOutSidecar) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    $finalReportsLines = [System.IO.File]::ReadAllLines($reportsSidecar) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    foreach ($line in $finalVerifOutLines) {
        $parts = $line -split '\s+', 2
        $expectedH = $parts[0].ToLowerInvariant()
        $relP = $parts[1]
        if ($relP -match '/(bin|obj|TestResults)/') {
            throw "Sidecar security violation: generated intermediate '$relP' must not be in sidecar manifest"
        }
        $fullP = Join-Path $gitRoot $relP
        if (-not (Test-Path $fullP)) {
            throw "Sidecar file missing on disk: $relP"
        }
        $actualH = (Get-FileHash -Path $fullP -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualH -ne $expectedH) {
            throw "Sidecar hash mismatch for $relP (Expected $expectedH, got $actualH)"
        }
    }

    foreach ($line in $finalReportsLines) {
        $parts = $line -split '\s+', 2
        $expectedH = $parts[0].ToLowerInvariant()
        $relP = $parts[1]
        $fullP = Join-Path $gitRoot $relP
        if (-not (Test-Path $fullP)) {
            throw "Reports sidecar file missing on disk: $relP"
        }
        $actualH = (Get-FileHash -Path $fullP -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actualH -ne $expectedH) {
            throw "Reports sidecar hash mismatch for $relP (Expected $expectedH, got $actualH)"
        }
    }
    Write-Host "[PASS] Sidecars verified: $($finalVerifOutLines.Count) verification outputs and $($finalReportsLines.Count) reports validated 100% on disk" -ForegroundColor Green

    # 8.4 Docker Cleanliness Check
    $postContainers = (& docker ps -a --filter "label=dxos.task=open_source-cab.4" --format "{{.ID}}|{{.Names}}")
    $postNetworks = (& docker network ls --filter "label=dxos.task=open_source-cab.4" --format "{{.ID}}|{{.Name}}")
    $postVolumes = (& docker volume ls --filter "label=dxos.task=open_source-cab.4" --format "{{.Name}}")

    if (-not [string]::IsNullOrWhiteSpace("$postContainers".Trim())) {
        throw "Post-child residue violation: Task-labeled containers remain: $postContainers"
    }
    if (-not [string]::IsNullOrWhiteSpace("$postNetworks".Trim())) {
        throw "Post-child residue violation: Task-labeled networks remain: $postNetworks"
    }
    if (-not [string]::IsNullOrWhiteSpace("$postVolumes".Trim())) {
        throw "Post-child residue violation: Task-labeled volumes remain: $postVolumes"
    }
    Write-Host "[PASS] Docker cleanliness verified: Exactly 0 task-labeled containers, networks, volumes" -ForegroundColor Green

    # 8.5 OpenSpec & Beads Validation
    $openspecVal = (& openspec.cmd validate bootstrap-remediation-001 --strict 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Post-child OpenSpec validation failed: $openspecVal"
    }
    $tasksContent = [System.IO.File]::ReadAllText((Join-Path $gitRoot "openspec/changes/bootstrap-remediation-001/tasks.md"))
    if ($tasksContent -match '- \[x\] 5\.1' -or $tasksContent -match '- \[x\] 5\.2' -or $tasksContent -match '- \[x\] 5\.3' -or $tasksContent -match '- \[x\] 5\.4') {
        throw "OpenSpec state violation: Tasks R5.1-R5.4 must remain unchecked [ ] pending review"
    }

    $beadsOut = (& powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "bd show open_source-cab.4 --json" 2>&1)
    if ($LASTEXITCODE -ne 0) {
        throw "Beads command failed: $beadsOut"
    }
    $beadsObj = $beadsOut | ConvertFrom-Json
    $beadsIssue = if ($beadsObj -is [array]) { $beadsObj[0] } else { $beadsObj }
    if ($null -eq $beadsIssue -or $beadsIssue.id -ne "open_source-cab.4" -or $beadsIssue.status -ne "in_progress") {
        throw "Beads state violation: open_source-cab.4 must be in_progress"
    }
    Write-Host "[PASS] OpenSpec and Beads state verified: OpenSpec valid, Tasks R5 unchecked, Beads in_progress" -ForegroundColor Green

    # 8.6 Concurrency Lock Release (Prior to Final State Verification)
    if ($null -ne $lockFileStream) {
        $lockFileStream.Dispose()
        $lockFileStream = $null
        if (Test-Path $lockFilePath) {
            Remove-Item $lockFilePath -Force -ErrorAction Stop
        }
    }
    Write-Host "[PASS] Concurrency lock released and removed (.run.lock)" -ForegroundColor Green

    # -------------------------------------------------------------------------
    # 9. TERMINAL ATOMIC PUBLICATION & PROJECTED FINAL GIT ASSERTION
    # -------------------------------------------------------------------------
    $preparedTime = [DateTimeOffset]::UtcNow
    $elapsedSeconds = ($preparedTime - $wrapperStartTime).TotalSeconds

    $completionContent = @"
================================================================================
BR001-R5 EXTERNAL BOUNDED FINALIZER COMPLETION RECORD
================================================================================
Run GUID: $RunGuid
Evidence Directory: $runDir
Wrapper PID: $wrapperPid
Child PID: $childPid
Wrapper Start: $($wrapperStartTime.ToString("o"))
Completion Prepared At: $($preparedTime.ToString("o"))
Elapsed Through Pre-Publication Validation: $([Math]::Round($elapsedSeconds, 3))s
Child Start: $($childStartTime.ToString("o"))
Child End: $($childEndTime.ToString("o"))
Child Wall Duration: $([Math]::Round($childDurationSeconds, 3))s
Child Exit Code: $childExitCode

COMPLETED VALIDATIONS:
1. Strict Text Hygiene: PASS ($($uniqueScanFiles.Count) files verified, 0 violations)
2. Markdown Link Resolution: PASS ($verifiedLinksCount links resolved)
3. Primary Sidecar Manifest: PASS ($($finalVerifOutLines.Count) entries verified on disk)
4. Reports Sidecar Manifest: PASS ($($finalReportsLines.Count) entries verified on disk)
5. Docker Cleanliness: PASS (0 task-labeled containers, networks, volumes)
6. OpenSpec Strict Validation: PASS (Tasks R5.1-R5.4 confirmed unchecked [ ])
7. Beads Issue open_source-cab.4: PASS (Confirmed status == in_progress)
8. Concurrency Lock: PASS (Released and removed .run.lock)
================================================================================
FINALIZER VERDICT: FINALIZER_PREPARED_FOR_ATOMIC_PUBLICATION
================================================================================
"@

    $projectedGitValidator = {
        $postGit = (& git status --short --untracked-files=all)
        $expectedPostStatus = @(
            "M Directory.Packages.props",
            "M global.json",
            "M scripts/check-contract.json",
            "M scripts/check.ps1",
            "M scripts/verify-check-contract.ps1",
            "M src/DXOS.Api/packages.lock.json",
            "M src/DXOS.AppHost/packages.lock.json",
            "M src/DXOS.Application/packages.lock.json",
            "M src/DXOS.Domain/packages.lock.json",
            "M src/DXOS.Infrastructure/packages.lock.json",
            "M src/DXOS.Workflows/packages.lock.json",
            "M tests/DXOS.Architecture.Tests/DXOS.Architecture.Tests.csproj",
            "D tests/DXOS.Architecture.Tests/UnitTest1.cs",
            "M tests/DXOS.Architecture.Tests/packages.lock.json",
            "M tests/DXOS.Integration.Tests/DXOS.Integration.Tests.csproj",
            "D tests/DXOS.Integration.Tests/UnitTest1.cs",
            "M tests/DXOS.Integration.Tests/packages.lock.json",
            "D tests/DXOS.Unit.Tests/UnitTest1.cs",
            "M tests/DXOS.Unit.Tests/packages.lock.json",
            "?? docs/testing.md",
            "?? scripts/run-test-project.ps1",
            "?? tests/DXOS.Architecture.Tests/ArchitectureTests.cs",
            "?? tests/DXOS.Architecture.Tests/Fixtures/ViolatingDomainFixture.cs",
            "?? tests/DXOS.Architecture.Tests/Fixtures/ViolatingProjectReferenceFixture.cs",
            "?? tests/DXOS.Integration.Tests/ContainerTeardownFixtureTests.cs",
            "?? tests/DXOS.Integration.Tests/PostgresAndElsaIntegrationTests.cs",
            "?? tests/DXOS.Integration.Tests/Teardown/ContainerTeardownHelper.cs",
            "?? tests/DXOS.Unit.Tests/BootstrapDbContextFactoryTests.cs",
            "?? tests/DXOS.Unit.Tests/BootstrapDbContextModelTests.cs",
            "?? artifacts/task-runs/open_source-cab.4/finalizer-ordering-fixture.log",
            "?? artifacts/task-runs/open_source-cab.4/finalizer-publish-helper.ps1",
            "?? artifacts/task-runs/open_source-cab.4/implementation-report.md",
            "?? artifacts/task-runs/open_source-cab.4/package-and-license-delta.md",
            "?? artifacts/task-runs/open_source-cab.4/prompt.md",
            "?? artifacts/task-runs/open_source-cab.4/reports.sha256",
            "?? artifacts/task-runs/open_source-cab.4/run-r5-final-verification.ps1",
            "?? artifacts/task-runs/open_source-cab.4/run-r5-verification.ps1",
            "?? artifacts/task-runs/open_source-cab.4/test-finalizer-ordering-fixture.ps1",
            "?? artifacts/task-runs/open_source-cab.4/verification-output.sha256",
            "?? artifacts/task-runs/open_source-cab.4/verification.md"
        )

        $declarativeRunFiles = @(
            "step-01-Environment_Preflight___Baseline_Inventories.log",
            "step-02-OpenSpec_Validation.log",
            "step-03-Contract_Verifier.log",
            "step-04-Docker-Absence_Failure_Boundary___CTRF_Assertion.log",
            "step-05-Foundation_Quality_Gate.log",
            "step-06-Runtime_Quality_Gate.log",
            "step-07-Docker_Residue___Exact_State_Delta_Audit.log",
            "step-08-Lock_Immutability__Exact_Git_Status_Set___State_Audit.log",
            "step-09-PowerShell_Parser___Strict_Text_Hygiene_Audit.log",
            "step-10-Verification_Report___Complete_Sidecars.log",
            "docker-baseline-containers.txt",
            "docker-baseline-networks.txt",
            "docker-baseline-volumes.txt",
            "docker-post-containers.txt",
            "docker-post-networks.txt",
            "docker-post-volumes.txt",
            "docker-diff-evidence.json",
            "negative-docker-ctrf.json",
            "unit-test-ctrf.json",
            "arch-test-ctrf.json",
            "int-test-ctrf.json",
            "foundation-evidence.json",
            "runtime-evidence.json",
            "Foundation-foundation-restore.out.txt",
            "Foundation-foundation-format.out.txt",
            "Foundation-foundation-build.out.txt",
            "Foundation-foundation-hygiene.out.txt",
            "Foundation-foundation-openspec.out.txt",
            "Runtime-foundation-restore.out.txt",
            "Runtime-foundation-format.out.txt",
            "Runtime-foundation-build.out.txt",
            "Runtime-foundation-hygiene.out.txt",
            "Runtime-foundation-openspec.out.txt",
            "Runtime-runtime-unit-tests.out.txt",
            "Runtime-runtime-architecture-tests.out.txt",
            "Runtime-runtime-integration-tests.out.txt",
            "Runtime-runtime-smoke-aspire.out.txt",
            "Runtime-runtime-smoke-compose.out.txt",
            "Runtime-runtime-docker-compose.out.txt",
            "Runtime-runtime-e2e-tests.out.txt",
            "git-status.txt",
            "text-hygiene-report.json",
            "child-stdout.log",
            "child-stderr.log",
            "child-execution.log"
        )

        foreach ($drf in $declarativeRunFiles) {
            $expectedPostStatus += "?? artifacts/task-runs/open_source-cab.4/runs/$RunGuid/$drf"
        }

        # Projected after publication entries:
        $expectedPostStatus += "?? artifacts/task-runs/open_source-cab.4/runs/$RunGuid/.completion-staging/finalizer-completion.log"
        $expectedPostStatus += "?? artifacts/task-runs/open_source-cab.4/runs/$RunGuid/.completion-staging/finalizer-completion.sha256"

        $actualPostLines = @()
        foreach ($line in ($postGit -split "`r?`n")) {
            $trimmed = $line.Trim()
            if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
                $normLine = ($trimmed -replace '\\', '/')
                if ($normLine -ne "?? artifacts/task-runs/open_source-cab.4/review.md") {
                    $actualPostLines += $normLine
                }
            }
        }

        $actualNorm = $actualPostLines | Sort-Object
        $expectedNorm = $expectedPostStatus | Sort-Object

        $unexpected = @($actualNorm | Where-Object { $_ -notin $expectedNorm })
        $missing = @($expectedNorm | Where-Object { $_ -notin $actualNorm })

        if ($unexpected.Length -gt 0 -or $missing.Length -gt 0) {
            throw "Projected Git status verification failed!`nUnexpected ($($unexpected.Length)):`n$($unexpected -join "`n")`nMissing ($($missing.Length)):`n$($missing -join "`n")"
        }
        Write-Host "[PASS] Projected Git status verified: Exact set match ($($actualNorm.Length) entries, 0 unexpected, 0 missing)" -ForegroundColor Green
    }

    $relCompLog = "artifacts/task-runs/open_source-cab.4/runs/$RunGuid/completion/finalizer-completion.log"
    Publish-CompletionUnit -RunDir $runDir -CompletionLogContent $completionContent -RelLogPath $relCompLog -ProjectedGitValidator $projectedGitValidator

    Write-Host "[PASS] Terminal completion published atomically to $runDir/completion/" -ForegroundColor Green
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "BR001-R5 EXTERNAL BOUNDED FINALIZER COMPLETED SUCCESSFULLY" -ForegroundColor Green
    Write-Host "================================================================================" -ForegroundColor Cyan
    exit 0
} finally {
    if ($null -ne $lockFileStream) {
        $lockFileStream.Dispose()
        $lockFileStream = $null
        if (Test-Path $lockFilePath) {
            Remove-Item $lockFilePath -Force -ErrorAction Stop
        }
    }
}
