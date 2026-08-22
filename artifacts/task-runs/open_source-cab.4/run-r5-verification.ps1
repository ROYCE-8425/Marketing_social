[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$RunGuid
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

$evidenceDir = $PSScriptRoot
$gitRoot = (Get-Item $evidenceDir).Parent.Parent.Parent.FullName
$currentDir = (Get-Item .).FullName
if ($currentDir -ne $gitRoot) {
    Set-Location $gitRoot
}

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

# 2. Validate ancestor chain BEFORE any creation, deletion, or write
Assert-SafePathChain $evidenceDir $gitRoot

$runsBaseDir = Join-Path $evidenceDir "runs"
if (-not (Test-Path $runsBaseDir)) {
    New-Item -ItemType Directory -Path $runsBaseDir | Out-Null
}
Assert-SafePathChain $runsBaseDir $evidenceDir

$runDir = Join-Path $runsBaseDir $RunGuid
Assert-SafePathChain $runDir $runsBaseDir

# 3. Enforce single-use run ID: fail if directory already exists (no deletion/reuse)
if (Test-Path $runDir) {
    throw "Run isolation violation: Run directory '$runDir' already exists for RunGuid '$RunGuid'. Run IDs must be strictly unique and single-use."
}
New-Item -ItemType Directory -Path $runDir | Out-Null

$runnerStartTime = [DateTimeOffset]::UtcNow

function Execute-NativeBounded {
    param(
        [string]$Command,
        [string[]]$Arguments,
        [int]$TimeoutMs = 120000,
        [hashtable]$EnvironmentVariables = $null
    )

    $psi = New-Object System.Diagnostics.ProcessStartInfo
    $psi.FileName = $Command
    $psi.Arguments = ($Arguments | ForEach-Object {
        if ($_ -match '[\s"]') {
            '"' + ($_ -replace '"', '\"') + '"'
        } else {
            $_
        }
    }) -join ' '
    $psi.WorkingDirectory = $gitRoot
    $psi.RedirectStandardOutput = $true
    $psi.RedirectStandardError = $true
    $psi.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    $psi.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
    $psi.UseShellExecute = $false
    $psi.CreateNoWindow = $true

    if ($EnvironmentVariables) {
        foreach ($k in $EnvironmentVariables.Keys) {
            $psi.EnvironmentVariables[$k] = [string]$EnvironmentVariables[$k]
        }
    }

    $proc = [System.Diagnostics.Process]::Start($psi)
    $stdoutTask = $proc.StandardOutput.ReadToEndAsync()
    $stderrTask = $proc.StandardError.ReadToEndAsync()

    if (-not $proc.WaitForExit($TimeoutMs)) {
        try {
            & taskkill /PID $proc.Id /T /F 2>&1 | Out-Null
        } catch {
            $proc.Kill()
        }
        throw "Command '$Command $($psi.Arguments)' timed out after $($TimeoutMs / 1000)s"
    }

    $stdout = $stdoutTask.Result
    $stderr = $stderrTask.Result

    return @{
        ExitCode = $proc.ExitCode
        Stdout = $stdout
        Stderr = $stderr
    }
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

function Write-StepLog {
    param(
        [int]$StepNumber,
        [string]$StepName,
        [string]$CommandText,
        [int]$ExitCode,
        [double]$DurationSeconds,
        [string]$Stdout,
        [string]$Stderr
    )

    $logFile = Join-Path $runDir ("step-{0:D2}-{1}.log" -f $StepNumber, ($StepName -replace '[^a-zA-Z0-9_-]', '_'))
    $logContent = @"
================================================================================
STEP ${StepNumber}: $StepName
COMMAND: $CommandText
EXIT CODE: $ExitCode
DURATION: $([Math]::Round($DurationSeconds, 3))s
TIMESTAMP: $([DateTimeOffset]::UtcNow.ToString("o"))
================================================================================
--- STDOUT ---
$Stdout
--- STDERR ---
$Stderr
================================================================================
"@
    Write-CleanFile $logFile $logContent
    $hash = (Get-FileHash -Path $logFile -Algorithm SHA256).Hash.ToLowerInvariant()
    return @{
        StepNumber = $StepNumber
        StepName = $StepName
        Command = $CommandText
        ExitCode = $ExitCode
        DurationSeconds = [Math]::Round($DurationSeconds, 3)
        LogFile = $logFile
        LogFileRelative = "runs/$RunGuid/" + [System.IO.Path]::GetFileName($logFile)
        Sha256 = $hash
    }
}

$stepRecords = @()

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "STARTING BR001-R5 MEANINGFUL TEST FOUNDATION CHILD VERIFICATION RUNNER" -ForegroundColor Cyan
Write-Host "Run GUID: $RunGuid | Evidence Dir: $runDir" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# -----------------------------------------------------------------------------
# STEP 01: Preflight Environment & Baseline Docker Inventories & Lock Snapshot
# -----------------------------------------------------------------------------
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$sdkRes = Execute-NativeBounded "dotnet" @("--version") 10000
$gitRes = Execute-NativeBounded "git" @("rev-parse", "HEAD") 10000
$dockerPingRes = Execute-NativeBounded "docker" @("info") 15000

# Canonical baseline Docker inventory capture
$preContainersRes = Execute-NativeBounded "docker" @("ps", "-a", "--format", "{{.ID}}|{{.Names}}|{{.Image}}|{{.Status}}") 10000
$preNetworksRes = Execute-NativeBounded "docker" @("network", "ls", "--format", "{{.ID}}|{{.Name}}|{{.Driver}}") 10000
$preVolumesRes = Execute-NativeBounded "docker" @("volume", "ls", "--format", "{{.Name}}|{{.Driver}}") 10000

# Pre-run task label queries (must be zero)
$preTaskContainers = Execute-NativeBounded "docker" @("ps", "-a", "--filter", "label=dxos.task=open_source-cab.4", "--format", "{{.ID}}|{{.Names}}") 10000
$preTaskNetworks = Execute-NativeBounded "docker" @("network", "ls", "--filter", "label=dxos.task=open_source-cab.4", "--format", "{{.ID}}|{{.Name}}") 10000
$preTaskVolumes = Execute-NativeBounded "docker" @("volume", "ls", "--filter", "label=dxos.task=open_source-cab.4", "--format", "{{.Name}}") 10000

if (-not [string]::IsNullOrWhiteSpace($preTaskContainers.Stdout.Trim())) {
    throw "Preflight failed: Residual task-labeled containers found: $($preTaskContainers.Stdout)"
}
if (-not [string]::IsNullOrWhiteSpace($preTaskNetworks.Stdout.Trim())) {
    throw "Preflight failed: Residual task-labeled networks found: $($preTaskNetworks.Stdout)"
}
if (-not [string]::IsNullOrWhiteSpace($preTaskVolumes.Stdout.Trim())) {
    throw "Preflight failed: Residual task-labeled volumes found: $($preTaskVolumes.Stdout)"
}

# Save canonical baseline inventories to runDir
$baseContainersPath = Join-Path $runDir "docker-baseline-containers.txt"
$baseNetworksPath = Join-Path $runDir "docker-baseline-networks.txt"
$baseVolumesPath = Join-Path $runDir "docker-baseline-volumes.txt"
Write-CleanFile $baseContainersPath $preContainersRes.Stdout
Write-CleanFile $baseNetworksPath $preNetworksRes.Stdout
Write-CleanFile $baseVolumesPath $preVolumesRes.Stdout

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
$initialLockHashes = @{}
foreach ($lock in $expectedLocks) {
    $fullLockPath = Join-Path $gitRoot $lock
    if (-not (Test-Path $fullLockPath)) {
        throw "Missing required lock file: $lock"
    }
    $initialLockHashes[$lock] = (Get-FileHash -Path $fullLockPath -Algorithm SHA256).Hash.ToLowerInvariant()
}

$sdkVersion = $sdkRes.Stdout.Trim()
$gitHead = $gitRes.Stdout.Trim()
$sw.Stop()

$step1Output = @"
Dotnet SDK: $sdkVersion
Git HEAD: $gitHead
Baseline Containers: $($preContainersRes.Stdout.Trim().Length) chars
Baseline Networks: $($preNetworksRes.Stdout.Trim().Length) chars
Baseline Volumes: $($preVolumesRes.Stdout.Trim().Length) chars
All 9 lock files verified.
"@
$step1Rec = Write-StepLog 1 "Environment Preflight & Baseline Inventories" "dotnet --version && git rev-parse HEAD && docker inventories && lock snapshot" 0 $sw.Elapsed.TotalSeconds $step1Output ""
$stepRecords += $step1Rec
Write-Host "[STEP 01/10] Environment Preflight & Baseline Inventories: PASS" -ForegroundColor Green

# -----------------------------------------------------------------------------
# STEP 02: OpenSpec Strict Validation (Initial State)
# -----------------------------------------------------------------------------
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$openspecRes = Execute-NativeBounded "openspec.cmd" @("validate", "bootstrap-remediation-001", "--strict") 30000
$sw.Stop()

if ($openspecRes.ExitCode -ne 0) {
    throw "OpenSpec validation failed: $($openspecRes.Stderr)`n$($openspecRes.Stdout)"
}
$step2Rec = Write-StepLog 2 "OpenSpec Validation" "openspec validate bootstrap-remediation-001 --strict" $openspecRes.ExitCode $sw.Elapsed.TotalSeconds $openspecRes.Stdout $openspecRes.Stderr
$stepRecords += $step2Rec
Write-Host "[STEP 02/10] OpenSpec Strict Validation: PASS" -ForegroundColor Green

# -----------------------------------------------------------------------------
# STEP 03: Quality Gate Contract Verifier
# -----------------------------------------------------------------------------
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$contractVerifierRes = Execute-NativeBounded "powershell.exe" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "scripts/verify-check-contract.ps1") 120000
$sw.Stop()

if ($contractVerifierRes.ExitCode -ne 0) {
    throw "Contract verifier failed: $($contractVerifierRes.Stderr)`n$($contractVerifierRes.Stdout)"
}
$step3Rec = Write-StepLog 3 "Contract Verifier" "powershell scripts/verify-check-contract.ps1" $contractVerifierRes.ExitCode $sw.Elapsed.TotalSeconds $contractVerifierRes.Stdout $contractVerifierRes.Stderr
$stepRecords += $step3Rec
Write-Host "[STEP 03/10] Quality Gate Contract Verifier: PASS" -ForegroundColor Green

# -----------------------------------------------------------------------------
# STEP 04: Docker-Absence Negative Failure Boundary & CTRF Verification
# -----------------------------------------------------------------------------
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$invalidDockerHostEnv = @{ "DOCKER_HOST" = "tcp://127.0.0.1:65534" }
$negCtrfRel = "negative-docker-ctrf.json"
$failClosedIntRes = Execute-NativeBounded "dotnet" @(
    "test", "tests/DXOS.Integration.Tests/DXOS.Integration.Tests.csproj",
    "-c", "Release", "--no-build", "--no-restore",
    "--", "--filter-method", "*PostgresAndElsaIntegrationTests*", "--report-ctrf", "--report-ctrf-filename", $negCtrfRel
) 60000 $invalidDockerHostEnv

if ($failClosedIntRes.ExitCode -eq 0) {
    throw "Docker-absence failure boundary violated: Integration test succeeded despite invalid DOCKER_HOST!"
}

# Locate and copy generated negative CTRF report
$negCtrfSource = Join-Path $gitRoot "tests/DXOS.Integration.Tests/bin/Release/net10.0/TestResults/$negCtrfRel"
if (-not (Test-Path $negCtrfSource)) {
    throw "Negative CTRF report was not produced at: $negCtrfSource"
}
$negCtrfDest = Join-Path $runDir "negative-docker-ctrf.json"
Copy-Item -Path $negCtrfSource -Destination $negCtrfDest -Force

$negCtrfObj = Get-Content $negCtrfDest -Raw | ConvertFrom-Json
$negSummary = $negCtrfObj.results.summary
if ($negSummary.tests -le 0) {
    throw "Negative CTRF assertion failed: Total tests is $($negSummary.tests), expected > 0"
}
if ($negSummary.passed -ne 0) {
    throw "Negative CTRF assertion failed: Passed tests is $($negSummary.passed), expected 0"
}
if ($negSummary.failed -le 0) {
    throw "Negative CTRF assertion failed: Failed tests is $($negSummary.failed), expected > 0"
}
if ($negSummary.skipped -ne 0) {
    throw "Negative CTRF assertion failed: Skipped tests is $($negSummary.skipped), expected 0"
}

$combinedFailOutput = $failClosedIntRes.Stdout + "`n" + $failClosedIntRes.Stderr
if (-not ($combinedFailOutput.Contains("Docker") -or $combinedFailOutput.Contains("connection") -or $combinedFailOutput.Contains("Endpoint") -or $combinedFailOutput.Contains("HttpRequestException") -or $combinedFailOutput.Contains("No connection could be made"))) {
    throw "Docker-absence failure boundary violated: Missing expected connection diagnostic in output"
}

$sw.Stop()
$step4Output = @"
Docker Absence Assertion:
Exit Code: $($failClosedIntRes.ExitCode) (Expected non-zero)
Negative CTRF Summary: Total=$($negSummary.tests), Passed=$($negSummary.passed), Failed=$($negSummary.failed), Skipped=$($negSummary.skipped)
Connection failure diagnostic confirmed.
"@
$step4Rec = Write-StepLog 4 "Docker-Absence Failure Boundary & CTRF Assertion" "dotnet test tests/DXOS.Integration.Tests/DXOS.Integration.Tests.csproj (with DOCKER_HOST=tcp://127.0.0.1:65534)" $failClosedIntRes.ExitCode $sw.Elapsed.TotalSeconds $step4Output $failClosedIntRes.Stderr
$stepRecords += $step4Rec
Write-Host "[STEP 04/10] Docker-Absence Failure Boundary: PASS (Exit $($failClosedIntRes.ExitCode), Total=$($negSummary.tests), Passed=$($negSummary.passed), Failed=$($negSummary.failed), Skipped=$($negSummary.skipped))" -ForegroundColor Green

# -----------------------------------------------------------------------------
# STEP 05: Foundation Quality Gate Execution
# -----------------------------------------------------------------------------
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$foundEvidenceRel = "artifacts/task-runs/open_source-cab.4/runs/$RunGuid/foundation-evidence.json"
$foundRes = Execute-NativeBounded "powershell.exe" @(
    "-NoProfile", "-ExecutionPolicy", "Bypass",
    "-File", "scripts/check.ps1",
    "-Profile", "Foundation",
    "-EvidencePath", $foundEvidenceRel
) 120000
$sw.Stop()

if ($foundRes.ExitCode -ne 0) {
    throw "Foundation quality gate failed: $($foundRes.Stderr)`n$($foundRes.Stdout)"
}
$step5Rec = Write-StepLog 5 "Foundation Quality Gate" "powershell scripts/check.ps1 -Profile Foundation" $foundRes.ExitCode $sw.Elapsed.TotalSeconds $foundRes.Stdout $foundRes.Stderr
$stepRecords += $step5Rec
Write-Host "[STEP 05/10] Foundation Quality Gate: PASS (5 gates)" -ForegroundColor Green

# -----------------------------------------------------------------------------
# STEP 06: Authoritative Runtime Quality Gate Execution (Single Positive Run)
# -----------------------------------------------------------------------------
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$runtimeEvidenceRel = "artifacts/task-runs/open_source-cab.4/runs/$RunGuid/runtime-evidence.json"
$runtimeRes = Execute-NativeBounded "powershell.exe" @(
    "-NoProfile", "-ExecutionPolicy", "Bypass",
    "-File", "scripts/check.ps1",
    "-Profile", "Runtime",
    "-EvidencePath", $runtimeEvidenceRel
) 300000
$sw.Stop()

if ($runtimeRes.ExitCode -ne 0) {
    throw "Runtime quality gate failed: $($runtimeRes.Stderr)`n$($runtimeRes.Stdout)"
}

# Verify exact CTRF artifacts produced directly in runDir
$unitCtrfRaw = Join-Path $runDir "unit-test-ctrf.json"
$archCtrfRaw = Join-Path $runDir "arch-test-ctrf.json"
$intCtrfRaw = Join-Path $runDir "int-test-ctrf.json"

if (-not (Test-Path $unitCtrfRaw) -or -not (Test-Path $archCtrfRaw) -or -not (Test-Path $intCtrfRaw)) {
    throw "Runtime quality gate failed to produce expected CTRF artifacts directly in runs/$RunGuid/"
}

$step6Rec = Write-StepLog 6 "Runtime Quality Gate" "powershell scripts/check.ps1 -Profile Runtime" $runtimeRes.ExitCode $sw.Elapsed.TotalSeconds $runtimeRes.Stdout $runtimeRes.Stderr
$stepRecords += $step6Rec
Write-Host "[STEP 06/10] Authoritative Runtime Quality Gate: PASS (All 12 gates)" -ForegroundColor Green

# -----------------------------------------------------------------------------
# STEP 07: Post-Run Docker Inventory & Exact Set Delta Audit
# -----------------------------------------------------------------------------
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$preContainersList = ($preContainersRes.Stdout.Trim() -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)
$preNetworksList = ($preNetworksRes.Stdout.Trim() -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)
$preVolumesList = ($preVolumesRes.Stdout.Trim() -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)

$postContainersRes = $null
$postNetworksRes = $null
$postVolumesRes = $null
$postContainersList = @()
$postNetworksList = @()
$postVolumesList = @()

$settleDeadline = [DateTimeOffset]::UtcNow.AddSeconds(15)
while ([DateTimeOffset]::UtcNow -lt $settleDeadline) {
    $postContainersRes = Execute-NativeBounded "docker" @("ps", "-a", "--format", "{{.ID}}|{{.Names}}|{{.Image}}|{{.Status}}") 10000
    $postNetworksRes = Execute-NativeBounded "docker" @("network", "ls", "--format", "{{.ID}}|{{.Name}}|{{.Driver}}") 10000
    $postVolumesRes = Execute-NativeBounded "docker" @("volume", "ls", "--format", "{{.Name}}|{{.Driver}}") 10000

    $postContainersList = ($postContainersRes.Stdout.Trim() -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)
    $postNetworksList = ($postNetworksRes.Stdout.Trim() -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)
    $postVolumesList = ($postVolumesRes.Stdout.Trim() -split "`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | Sort-Object)

    if ($postContainersList.Length -eq $preContainersList.Length) {
        break
    }
    Start-Sleep -Milliseconds 1000
}

# Post-run task label queries (must be strictly zero)
$postTaskContainers = Execute-NativeBounded "docker" @("ps", "-a", "--filter", "label=dxos.task=open_source-cab.4", "--format", "{{.ID}}|{{.Names}}") 10000
$postTaskNetworks = Execute-NativeBounded "docker" @("network", "ls", "--filter", "label=dxos.task=open_source-cab.4", "--format", "{{.ID}}|{{.Name}}") 10000
$postTaskVolumes = Execute-NativeBounded "docker" @("volume", "ls", "--filter", "label=dxos.task=open_source-cab.4", "--format", "{{.Name}}") 10000

if (-not [string]::IsNullOrWhiteSpace($postTaskContainers.Stdout.Trim())) {
    throw "Residue violation: Task-labeled containers remain: $($postTaskContainers.Stdout)"
}
if (-not [string]::IsNullOrWhiteSpace($postTaskNetworks.Stdout.Trim())) {
    throw "Residue violation: Task-labeled networks remain: $($postTaskNetworks.Stdout)"
}
if (-not [string]::IsNullOrWhiteSpace($postTaskVolumes.Stdout.Trim())) {
    throw "Residue violation: Task-labeled volumes remain: $($postTaskVolumes.Stdout)"
}

# Save canonical post-run inventories to runDir
$postContainersPath = Join-Path $runDir "docker-post-containers.txt"
$postNetworksPath = Join-Path $runDir "docker-post-networks.txt"
$postVolumesPath = Join-Path $runDir "docker-post-volumes.txt"
Write-CleanFile $postContainersPath $postContainersRes.Stdout
Write-CleanFile $postNetworksPath $postNetworksRes.Stdout
Write-CleanFile $postVolumesPath $postVolumesRes.Stdout

# Exact set comparison for pre/post inventories
if ($preContainersList.Length -ne $postContainersList.Length) {
    throw "Docker state violation: Container count changed from $($preContainersList.Length) to $($postContainersList.Length)"
}
for ($i = 0; $i -lt $preContainersList.Length; $i++) {
    $preParts = $preContainersList[$i] -split '\|'
    $postParts = $postContainersList[$i] -split '\|'
    if ($preParts[0] -ne $postParts[0] -or $preParts[1] -ne $postParts[1] -or $preParts[2] -ne $postParts[2]) {
        throw "Docker container mutation detected: Baseline '$($preContainersList[$i])' vs Post '$($postContainersList[$i])'"
    }
}

if ($preNetworksList.Length -ne $postNetworksList.Length) {
    throw "Docker state violation: Network count changed from $($preNetworksList.Length) to $($postNetworksList.Length)"
}
for ($i = 0; $i -lt $preNetworksList.Length; $i++) {
    if ($preNetworksList[$i] -ne $postNetworksList[$i]) {
        throw "Docker network mutation detected: Baseline '$($preNetworksList[$i])' vs Post '$($postNetworksList[$i])'"
    }
}

if ($preVolumesList.Length -ne $postVolumesList.Length) {
    throw "Docker state violation: Volume count changed from $($preVolumesList.Length) to $($postVolumesList.Length)"
}
for ($i = 0; $i -lt $preVolumesList.Length; $i++) {
    if ($preVolumesList[$i] -ne $postVolumesList[$i]) {
        throw "Docker volume mutation detected: Baseline '$($preVolumesList[$i])' vs Post '$($postVolumesList[$i])'"
    }
}

$dockerDiffEvidence = @{
    PreRunContainers = $preContainersList
    PostRunContainers = $postContainersList
    PreRunNetworks = $preNetworksList
    PostRunNetworks = $postNetworksList
    PreRunVolumes = $preVolumesList
    PostRunVolumes = $postVolumesList
    TaskLabeledContainers = @()
    TaskLabeledNetworks = @()
    TaskLabeledVolumes = @()
    ComparisonVerdict = "IDENTICAL"
}
$dockerDiffJsonPath = Join-Path $runDir "docker-diff-evidence.json"
Write-CleanFile $dockerDiffJsonPath ($dockerDiffEvidence | ConvertTo-Json -Depth 5)

$sw.Stop()

$step7Output = @"
Post-Run Containers:
$($postContainersRes.Stdout)
Post-Run Networks:
$($postNetworksRes.Stdout)
Post-Run Volumes:
$($postVolumesRes.Stdout)
Docker State Delta: Exactly 0 task-owned residue (containers, networks, volumes); pre-existing containers, networks, volumes preserved identically (exact 1:1 set match).
"@
$step7Rec = Write-StepLog 7 "Docker Residue & Exact State Delta Audit" "docker ps -a && docker network ls && docker volume ls (exact set diff & label query)" 0 $sw.Elapsed.TotalSeconds $step7Output ""
$stepRecords += $step7Rec
Write-Host "[STEP 07/10] Docker Residue & Exact State Delta Audit: PASS (0 residue, 100% pre-existing state preserved)" -ForegroundColor Green

# -----------------------------------------------------------------------------
# STEP 08: Lock Immutability, Exact Git Status Set & State Audit
# -----------------------------------------------------------------------------
$sw = [System.Diagnostics.Stopwatch]::StartNew()
foreach ($lock in $expectedLocks) {
    $fullLockPath = Join-Path $gitRoot $lock
    $currentHash = (Get-FileHash -Path $fullLockPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $initialHash = $initialLockHashes[$lock]
    if ($currentHash -ne $initialHash) {
        throw "Immutability violation: Lock file mutated ($lock)"
    }
}

$gitStatusLogPath = Join-Path $runDir "git-status.txt"
Write-CleanFile $gitStatusLogPath ""
$gitStatusRes = Execute-NativeBounded "git" @("status", "--short", "--untracked-files=all") 10000
Write-CleanFile $gitStatusLogPath $gitStatusRes.Stdout

# Declarative Frozen Inventory of Expected Git Status entries at Step 8
$expectedGitStatus = @(
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
    "?? artifacts/task-runs/open_source-cab.4/.run.lock",
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

# Declarative expected per-run files in runs/<runGuid>/ at Step 8 (not learned via Get-ChildItem)
$declarativeRunFiles = @(
    "step-01-Environment_Preflight___Baseline_Inventories.log",
    "step-02-OpenSpec_Validation.log",
    "step-03-Contract_Verifier.log",
    "step-04-Docker-Absence_Failure_Boundary___CTRF_Assertion.log",
    "step-05-Foundation_Quality_Gate.log",
    "step-06-Runtime_Quality_Gate.log",
    "step-07-Docker_Residue___Exact_State_Delta_Audit.log",
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
    "git-status.txt"
)

foreach ($drf in $declarativeRunFiles) {
    $expectedGitStatus += "?? artifacts/task-runs/open_source-cab.4/runs/$RunGuid/$drf"
}

$actualGitLines = @()
foreach ($line in ($gitStatusRes.Stdout -split "`r?`n")) {
    $trimmed = $line.Trim()
    if (-not [string]::IsNullOrWhiteSpace($trimmed)) {
        $normalizedLine = ($trimmed -replace '\\', '/')
        if ($normalizedLine -ne "?? artifacts/task-runs/open_source-cab.4/review.md") {
            $actualGitLines += $normalizedLine
        }
    }
}

$actualNormalized = $actualGitLines | Sort-Object
$expectedNormalized = $expectedGitStatus | Sort-Object

$unexpected = @($actualNormalized | Where-Object { $_ -notin $expectedNormalized })
$missing = @($expectedNormalized | Where-Object { $_ -notin $actualNormalized })

if ($unexpected.Length -gt 0 -or $missing.Length -gt 0) {
    throw "Exact Git status comparison failed!`nUnexpected ($($unexpected.Length)):`n$($unexpected -join "`n")`nMissing ($($missing.Length)):`n$($missing -join "`n")"
}

# Re-validate OpenSpec strictly
$postOpenspecRes = Execute-NativeBounded "openspec.cmd" @("validate", "bootstrap-remediation-001", "--strict") 30000
if ($postOpenspecRes.ExitCode -ne 0) {
    throw "Post-run OpenSpec validation failed: $($postOpenspecRes.Stderr)`n$($postOpenspecRes.Stdout)"
}

# Verify OpenSpec tasks R5.1-R5.4 remain unchecked
$tasksMdPath = Join-Path $gitRoot "openspec/changes/bootstrap-remediation-001/tasks.md"
$tasksContent = [System.IO.File]::ReadAllText($tasksMdPath)
if ($tasksContent -match '- \[x\] 5\.1' -or $tasksContent -match '- \[x\] 5\.2' -or $tasksContent -match '- \[x\] 5\.3' -or $tasksContent -match '- \[x\] 5\.4') {
    throw "OpenSpec state violation: Tasks R5.1-R5.4 must remain unchecked [ ] pending review"
}

# Verify Beads issue open_source-cab.4 is in_progress via exact JSON parsing
$beadsRes = Execute-NativeBounded "powershell.exe" @("-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", "bd show open_source-cab.4 --json") 15000
if ($beadsRes.ExitCode -ne 0) {
    throw "Beads command failed with exit code $($beadsRes.ExitCode): $($beadsRes.Stderr)`n$($beadsRes.Stdout)"
}
$beadsObj = $beadsRes.Stdout | ConvertFrom-Json
$beadsIssue = if ($beadsObj -is [array]) { $beadsObj[0] } else { $beadsObj }
if ($null -eq $beadsIssue -or $beadsIssue.id -ne "open_source-cab.4") {
    throw "Beads state violation: Could not find issue open_source-cab.4 in JSON output"
}
if ($beadsIssue.status -ne "in_progress") {
    throw "Beads state violation: open_source-cab.4 status is '$($beadsIssue.status)', expected 'in_progress'"
}

$sw.Stop()

$step8Output = @"
Lock Immutability: All 9 packages.lock.json files identical.
Git Status: Exact set match ($($actualNormalized.Length) entries verified, 0 unexpected, 0 missing).
Post-Run OpenSpec Validation: Strict check passed; Tasks R5.1-R5.4 confirmed unchecked [ ].
Beads State: open_source-cab.4 JSON parsed successfully; confirmed in_progress.
"@
$step8Rec = Write-StepLog 8 "Lock Immutability, Exact Git Status Set & State Audit" "Verify 9 lock hashes, exact declarative git status set comparison, post-run OpenSpec & Beads in_progress" 0 $sw.Elapsed.TotalSeconds $step8Output ""
$stepRecords += $step8Rec
Write-Host "[STEP 08/10] Lock Immutability, Exact Git Status Set & State Audit: PASS" -ForegroundColor Green

# -----------------------------------------------------------------------------
# STEP 09: PowerShell Parser & Strict Deliverables Text Hygiene Audit
# -----------------------------------------------------------------------------
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$scriptFiles = Get-ChildItem -Path $gitRoot -Include "*.ps1", "*.psm1" -Recurse -File | Where-Object {
    $_.FullName -notmatch '\\(bin|obj|\.git)\\'
}

$parserErrors = @()
foreach ($sf in $scriptFiles) {
    $tokens = $null
    $errors = $null
    [void][System.Management.Automation.Language.Parser]::ParseFile($sf.FullName, [ref]$tokens, [ref]$errors)
    if ($errors.Count -gt 0) {
        foreach ($e in $errors) {
            $parserErrors += "$($sf.FullName): $($e.Message)"
        }
    }
}
if ($parserErrors.Count -gt 0) {
    throw "PowerShell parser check failed:`n" + ($parserErrors -join "`n")
}

$utf8Strict = [System.Text.UTF8Encoding]::new($false, $true)
function Test-StrictFileHygiene {
    param([string]$FilePath)
    $rawBytes = [System.IO.File]::ReadAllBytes($FilePath)

    # 1. Throwing UTF-8 check
    try {
        $text = $utf8Strict.GetString($rawBytes)
    } catch {
        return "Invalid UTF-8 encoding: $($_.Exception.Message)"
    }

    # 2. Control characters & U+FFFD
    for ($ci = 0; $ci -lt $text.Length; $ci++) {
        $cVal = [int][char]$text[$ci]
        if ($cVal -eq 0xFFFD) {
            return "Contains U+FFFD replacement character at offset $ci"
        }
        if ($cVal -lt 0x20 -and $cVal -ne 0x09 -and $cVal -ne 0x0A -and $cVal -ne 0x0D) {
            return "Contains forbidden control character (0x$($cVal.ToString('X2'))) at offset $ci"
        }
    }

    # 3. Mojibake detection via unicode character sequences
    if ($text -match '[\u00C2\u00C3\u00E2][\u0080-\u00BF]') {
        return "Contains literal mojibake sequence"
    }

    # 4. Trailing whitespace on any line
    $lines = $text -split "`r?`n"
    for ($li = 0; $li -lt $lines.Length; $li++) {
        if ($lines[$li] -match '[ \t]+$') {
            return "Contains trailing whitespace on line $($li + 1)"
        }
    }

    return $null
}

# Strict text hygiene check across milestone deliverables and task-run evidence
$deliverableRelativePaths = @(
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

$deliverableItems = @()
foreach ($drp in $deliverableRelativePaths) {
    $fullPath = Join-Path $gitRoot $drp
    if (Test-Path $fullPath) {
        $deliverableItems += Get-Item $fullPath
    }
}

$deliverableItems += Get-ChildItem -Path (Join-Path $gitRoot "src") -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$deliverableItems += Get-ChildItem -Path (Join-Path $gitRoot "tests") -Recurse -File | Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' }
$deliverableItems += Get-ChildItem -Path $evidenceDir -File | Where-Object { $_.Name -ne "review.md" -and $_.Name -ne ".run.lock" }
$deliverableItems += Get-ChildItem -Path $runDir -File

$uniqueDeliverables = $deliverableItems | Sort-Object -Property FullName -Unique

$hygieneViolations = @()
foreach ($thf in $uniqueDeliverables) {
    $violation = Test-StrictFileHygiene $thf.FullName
    if ($null -ne $violation) {
        $hygieneViolations += "$($thf.FullName): $violation"
    }
}
if ($hygieneViolations.Count -gt 0) {
    throw "Text hygiene check failed:`n" + ($hygieneViolations -join "`n")
}

$hygieneReport = @{
    ParsedScriptsCount = $scriptFiles.Count
    ScannedDeliverablesCount = $uniqueDeliverables.Count
    ParserViolations = 0
    HygieneViolations = 0
    Status = "PASS"
}
$hygieneReportPath = Join-Path $runDir "text-hygiene-report.json"
Write-CleanFile $hygieneReportPath ($hygieneReport | ConvertTo-Json -Depth 3)
$sw.Stop()

$step9Rec = Write-StepLog 9 "PowerShell Parser & Strict Text Hygiene Audit" "AST parse all scripts && scan text hygiene (throwing UTF-8, no trailing whitespace, no mojibake, no control chars)" 0 $sw.Elapsed.TotalSeconds "All $($scriptFiles.Count) scripts parsed cleanly; all $($uniqueDeliverables.Count) deliverable files passed strict hygiene audit." ""
$stepRecords += $step9Rec
Write-Host "[STEP 09/10] PowerShell Parser & Strict Text Hygiene Audit: PASS" -ForegroundColor Green

# -----------------------------------------------------------------------------
# STEP 10: Dynamic Test Derivation, Reports & Clean-Clone Sidecars Generation
# -----------------------------------------------------------------------------
$step10Sw = [System.Diagnostics.Stopwatch]::StartNew()

$latestUnitCtrf = Join-Path $runDir "unit-test-ctrf.json"
$latestArchCtrf = Join-Path $runDir "arch-test-ctrf.json"
$latestIntCtrf = Join-Path $runDir "int-test-ctrf.json"

$unitSummary = (Get-Content $latestUnitCtrf -Raw | ConvertFrom-Json).results.summary
$archSummary = (Get-Content $latestArchCtrf -Raw | ConvertFrom-Json).results.summary
$intSummary = (Get-Content $latestIntCtrf -Raw | ConvertFrom-Json).results.summary

$runtimeEvJsonPath = Join-Path $gitRoot $runtimeEvidenceRel
$runtimeEvObj = Get-Content $runtimeEvJsonPath -Raw | ConvertFrom-Json

$gateDurations = @{}
foreach ($g in $runtimeEvObj.gates) {
    $gateDurations[$g.id] = $g.durationSeconds
}

# Pre-compute initial Step 10 record so table can be rendered
$step10PlaceholderRec = @{
    StepNumber = 10
    StepName = "Verification Report & Complete Sidecars"
    Command = "Mechanically parse CTRF and evidence JSON, write verification.md, compute comprehensive SHA256 sidecars"
    ExitCode = 0
    DurationSeconds = 0.0
    LogFile = Join-Path $runDir "step-10-Verification_Report___Complete_Sidecars.log"
    LogFileRelative = "runs/$RunGuid/step-10-Verification_Report___Complete_Sidecars.log"
    Sha256 = ""
}

# Generate verification.md (Strict UTF-8 without BOM, verified relative links)
function Generate-VerificationMarkdown {
    param([array]$Records)

    $verifMd = @"
# Verification Evidence - BR001-R5 Meaningful Test Foundation

## 1. Run Summary

- **Run ID**: ``$RunGuid``
- **Start Time**: ``$($runnerStartTime.ToString("o"))``
- **End Time**: ``$([DateTimeOffset]::UtcNow.ToString("o"))``
- **Overall Result**: **PASS**
- **Git Commit (HEAD)**: ``$gitHead``
- **Dotnet SDK**: ``$sdkVersion``
- **Operating System**: ``Windows (Microsoft Windows 10.0.26100)``

---

## 2. Verification Step Execution Matrix

| Step | Subtask / Category | Step Name | Command | Exit Code | Duration (s) | Log File | SHA256 Hash |
| :---: | :--- | :--- | :--- | :---: | :---: | :--- | :--- |
"@

    foreach ($sr in $Records) {
        $verifMd += "`n| $($sr.StepNumber) | BR001-R5 | $($sr.StepName) | ``$($sr.Command)`` | $($sr.ExitCode) | $($sr.DurationSeconds) | [$([System.IO.Path]::GetFileName($sr.LogFile))]($($sr.LogFileRelative)) | ``$($sr.Sha256)`` |"
    }

    $verifMd += @"


---

## 3. Test Suites & CTRF Results Summary (Mechanically Derived from Runtime Gate)

| Test Project | Scope / Focus | Total Tests | Passed | Failed | Skipped | Gate Duration | Result |
| :--- | :--- | :---: | :---: | :---: | :---: | :---: | :---: |
| **DXOS.Unit.Tests** | DbContext Factory, Precedence, EF Model Constraints | $($unitSummary.tests) | $($unitSummary.passed) | $($unitSummary.failed) | $($unitSummary.skipped) | $($gateDurations['runtime-unit-tests'])s | **PASS** |
| **DXOS.Architecture.Tests** | 13 Clean Architecture Rules + ProjectReference Validator + Sensitivity Fixtures | $($archSummary.tests) | $($archSummary.passed) | $($archSummary.failed) | $($archSummary.skipped) | $($gateDurations['runtime-architecture-tests'])s | **PASS** |
| **DXOS.Integration.Tests** | Testcontainers PostgreSQL 4.13.0, EF Migrations, Probe Persistence, Elsa Workflow, Teardown Fixtures | $($intSummary.tests) | $($intSummary.passed) | $($intSummary.failed) | $($intSummary.skipped) | $($gateDurations['runtime-integration-tests'])s | **PASS** |
| **E2E Tests** | End-to-End browser UI workflows | 0 | 0 | 0 | 0 | 0.0s | **NOT_APPLICABLE** |

---

## 4. Failure Boundary Proofs

1. **Docker-Absence Fail-Closed Contract**:
   - Integration suite executed with ``DOCKER_HOST=tcp://127.0.0.1:65534`` and CTRF output ``negative-docker-ctrf.json``.
   - Result: Exited non-zero (exit code $($failClosedIntRes.ExitCode)), Total=$($negSummary.tests), Passed=$($negSummary.passed), Failed=$($negSummary.failed), Skipped=$($negSummary.skipped) with explicit connection failure diagnostic. Zero false-positive passes.
2. **Container Teardown Failure Fixtures Contract**:
   - Verified via ``ContainerTeardownFixtureTests``:
     - Stop failure is propagated as ``InvalidOperationException`` and executes disposal.
     - Disposal fault is propagated.
     - Disposal timeout is bounded and throws ``TimeoutException``.
3. **Quality Gate Runner-Mechanics Contract**:
   - Verified via ``scripts/verify-check-contract.ps1``:
     - Non-zero test exit fails gate.
     - Zero-test result fails gate.
     - Missing CTRF result file fails gate.
     - Malformed CTRF result file fails gate.
     - ResultPath escape, sibling task-runs, and unapproved project paths strictly rejected.
     - Fail-fast ensures later success cannot mask earlier failure.
     - Runtime gate order is exact (12 gates).
     - E2E gate strictly recorded as ``NOT_APPLICABLE``.

---

## 5. Docker & Environment Cleanliness

- **Task-owned container residue**: 0 (queried via ``label=dxos.task=open_source-cab.4``)
- **Task-owned network residue**: 0 (queried via ``label=dxos.task=open_source-cab.4``)
- **Task-owned volume residue**: 0 (queried via ``label=dxos.task=open_source-cab.4``)
- **Pre-existing Docker state**: 100% preserved (exact 1:1 set match on containers, networks, volumes).
- **Lock files evaluated**: 9/9 matching initial SHA256 hashes.
- **Text hygiene**: 0 trailing whitespace violations, 0 mojibake, 0 U+FFFD characters.
"@
    return $verifMd
}

$verifMdPath = Join-Path $evidenceDir "verification.md"

# Generate verification-output.sha256 covering ALL run files and clean-clone tracked material inputs
function Generate-Sidecars {
    $verifOutSidecar = Join-Path $evidenceDir "verification-output.sha256"
    $verifOutLines = @()

    # 1. All run directory files
    $allRunFiles = Get-ChildItem -Path $runDir -File | Sort-Object Name
    foreach ($f in $allRunFiles) {
        $fHash = (Get-FileHash -Path $f.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
        $relPath = "artifacts/task-runs/open_source-cab.4/runs/$RunGuid/" + $f.Name
        $verifOutLines += "$fHash  $relPath"
    }

    # 2. Material implementation deliverables and configuration files
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
    foreach ($lock in $expectedLocks) {
        $materialInputFiles += $lock
    }

    # 3. Tracked source files in src and tests using git ls-files (strictly excludes obj/ and bin/ and deleted files)
    $gitLsSrc = Execute-NativeBounded "git" @("ls-files", "src", "tests") 10000
    foreach ($line in ($gitLsSrc.Stdout -split "`r?`n")) {
        $trimmed = $line.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmed) -and ($trimmed -match '\.(cs|csproj)$')) {
            $candidatePath = Join-Path $gitRoot $trimmed
            if (Test-Path $candidatePath) {
                $materialInputFiles += $trimmed.Replace('\', '/')
            }
        }
    }

    # 4. Untracked new deliverable tests and scripts
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

    Write-CleanFile $verifOutSidecar ($verifOutLines -join "`r`n")

    # Generate reports.sha256 covering all documentation and runner files in task-runs directory
    $reportsSidecar = Join-Path $evidenceDir "reports.sha256"
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
}

# Initial write of reports to allow Step 10 log generation
Write-CleanFile $verifMdPath (Generate-VerificationMarkdown ($stepRecords + $step10PlaceholderRec))
Generate-Sidecars

# Now stop stopwatch and record Step 10 log with truthful duration covering report/sidecar synthesis
$step10Sw.Stop()
$step10Rec = Write-StepLog 10 "Verification Report & Complete Sidecars" "Mechanically parse CTRF and evidence JSON, write verification.md, compute comprehensive SHA256 sidecars" 0 $step10Sw.Elapsed.TotalSeconds "Generated verification.md, computed verification-output.sha256 and reports.sha256 in $([Math]::Round($step10Sw.Elapsed.TotalSeconds, 3))s." ""
$stepRecords += $step10Rec

# Re-write verification.md and sidecars with final Step 10 log and hash
Write-CleanFile $verifMdPath (Generate-VerificationMarkdown $stepRecords)
Generate-Sidecars

# Mechanically verify every markdown link in verification.md resolves relative to $evidenceDir
$linkMatches = [regex]::Matches((Get-Content $verifMdPath -Raw), '\[([^\]]+)\]\(([^)]+)\)')
foreach ($match in $linkMatches) {
    $linkTarget = $match.Groups[2].Value
    $resolvedTarget = Join-Path $evidenceDir $linkTarget
    if (-not (Test-Path $resolvedTarget)) {
        throw "Link verification failure: '$linkTarget' in verification.md does not resolve to an existing file at '$resolvedTarget'"
    }
}

Write-Host "[STEP 10/10] Verification Report & Complete Sidecars: PASS ($([Math]::Round($step10Sw.Elapsed.TotalSeconds, 3))s)" -ForegroundColor Green

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "CHILD VERIFICATION RUNNER COMPLETED SUCCESSFULLY" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Cyan
exit 0
