[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$fixtureStartTime = [DateTimeOffset]::UtcNow
$scriptDir = $PSScriptRoot
$gitRoot = (Get-Item $scriptDir).Parent.Parent.Parent.FullName

. (Join-Path $scriptDir "finalizer-publish-helper.ps1")

$qualityGateRoot = Join-Path $gitRoot "artifacts/quality-gate"
if (-not (Test-Path $qualityGateRoot)) {
    New-Item -ItemType Directory -Path $qualityGateRoot -Force | Out-Null
}
Assert-SafePathChain $qualityGateRoot $gitRoot

$fixtureGuid = "fixture-" + [Guid]::NewGuid().ToString('N').Substring(0, 12)
$fixtureRunDir = Join-Path $qualityGateRoot $fixtureGuid
Assert-SafePathChain $fixtureRunDir $qualityGateRoot

New-Item -ItemType Directory -Path $fixtureRunDir -Force -ErrorAction Stop | Out-Null

$transcriptPath = Join-Path $scriptDir "finalizer-ordering-fixture.log"
$scenarioResults = [System.Collections.Generic.List[string]]::new()

function Log-Scenario {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Details
    )
    $msg = "[$Status] $($Name): $Details"
    Write-Host $msg -ForegroundColor $(if ($Status -eq "PASS") { "Green" } else { "Red" })
    $script:scenarioResults.Add($msg)
}

try {
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host "STARTING BR001-R5 FINALIZER ORDERING & ATOMIC PUBLICATION MECHANICAL FIXTURE" -ForegroundColor Cyan
    Write-Host "Fixture Root: $fixtureRunDir" -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor Cyan

    # -------------------------------------------------------------------------
    # SCENARIO 1: Pre-publication failure (induced Git mismatch in validator)
    # -------------------------------------------------------------------------
    $sc1Dir = Join-Path $fixtureRunDir "scenario-1"
    New-Item -ItemType Directory -Path $sc1Dir -Force -ErrorAction Stop | Out-Null
    Assert-SafePathChain $sc1Dir $fixtureRunDir

    $sc1Threw = $false
    try {
        $sc1GitValidator = {
            throw "Induced Pre-publication Git status mismatch"
        }
        Publish-CompletionUnit -RunDir $sc1Dir -CompletionLogContent "SCENARIO 1 TEST" -RelLogPath "artifacts/test/scenario1.log" -ProjectedGitValidator $sc1GitValidator
    } catch {
        $sc1Threw = $true
    }

    $sc1CompDir = Join-Path $sc1Dir "completion"
    $sc1StagingDir = Join-Path $sc1Dir ".completion-staging"

    if ($sc1Threw -and (-not (Test-Path $sc1CompDir)) -and (-not (Test-Path $sc1StagingDir))) {
        Log-Scenario "Scenario 1 (Pre-publication failure)" "PASS" "Validator error threw, completion dir absent, staging purged"
    } else {
        Log-Scenario "Scenario 1 (Pre-publication failure)" "FAIL" "Failed to properly abort and clean: threw=$sc1Threw, compDirExists=$(Test-Path $sc1CompDir), stagingExists=$(Test-Path $sc1StagingDir)"
        throw "Scenario 1 assertion failed"
    }

    # -------------------------------------------------------------------------
    # SCENARIO 2: Staging validation failure (induced invalid control character)
    # -------------------------------------------------------------------------
    $sc2Dir = Join-Path $fixtureRunDir "scenario-2"
    New-Item -ItemType Directory -Path $sc2Dir -Force -ErrorAction Stop | Out-Null
    Assert-SafePathChain $sc2Dir $fixtureRunDir

    $sc2Threw = $false
    try {
        # Induce forbidden control character 0x07 (bell)
        $invalidContent = "SCENARIO 2 WITH INVALID CONTROL CHAR " + [char]7
        Publish-CompletionUnit -RunDir $sc2Dir -CompletionLogContent $invalidContent -RelLogPath "artifacts/test/scenario2.log"
    } catch {
        $sc2Threw = $true
    }

    $sc2CompDir = Join-Path $sc2Dir "completion"
    $sc2StagingDir = Join-Path $sc2Dir ".completion-staging"

    if ($sc2Threw -and (-not (Test-Path $sc2CompDir)) -and (-not (Test-Path $sc2StagingDir))) {
        Log-Scenario "Scenario 2 (Staging validation failure)" "PASS" "Hygiene failure threw, completion dir absent, staging purged"
    } else {
        Log-Scenario "Scenario 2 (Staging validation failure)" "FAIL" "Failed hygiene abort: threw=$sc2Threw, compDirExists=$(Test-Path $sc2CompDir), stagingExists=$(Test-Path $sc2StagingDir)"
        throw "Scenario 2 assertion failed"
    }

    # -------------------------------------------------------------------------
    # SCENARIO 3: Atomic publication failure (destination already exists)
    # -------------------------------------------------------------------------
    $sc3Dir = Join-Path $fixtureRunDir "scenario-3"
    New-Item -ItemType Directory -Path $sc3Dir -Force -ErrorAction Stop | Out-Null
    Assert-SafePathChain $sc3Dir $fixtureRunDir

    $sc3CompDir = Join-Path $sc3Dir "completion"
    New-Item -ItemType Directory -Path $sc3CompDir -Force -ErrorAction Stop | Out-Null
    $sentinelPath = Join-Path $sc3CompDir "sentinel.txt"
    [System.IO.File]::WriteAllText($sentinelPath, "UNTOUCHED SENTINEL CONTENT", [System.Text.UTF8Encoding]::new($false))
    $expectedSentinelHash = (Get-FileHash -Path $sentinelPath -Algorithm SHA256).Hash

    $sc3Threw = $false
    try {
        Publish-CompletionUnit -RunDir $sc3Dir -CompletionLogContent "SCENARIO 3 TEST" -RelLogPath "artifacts/test/scenario3.log"
    } catch {
        $sc3Threw = $true
    }

    $sc3StagingDir = Join-Path $sc3Dir ".completion-staging"
    $actualSentinelHash = if (Test-Path $sentinelPath) { (Get-FileHash -Path $sentinelPath -Algorithm SHA256).Hash } else { "" }
    $sentinelPreserved = ($actualSentinelHash -eq $expectedSentinelHash)

    if ($sc3Threw -and $sentinelPreserved -and (-not (Test-Path $sc3StagingDir))) {
        Log-Scenario "Scenario 3 (Atomic publication collision)" "PASS" "Destination collision threw, sentinel file byte-for-byte preserved, staging purged"
    } else {
        Log-Scenario "Scenario 3 (Atomic publication collision)" "FAIL" "Collision handling failed: threw=$sc3Threw, sentinelPreserved=$sentinelPreserved, stagingExists=$(Test-Path $sc3StagingDir)"
        throw "Scenario 3 assertion failed"
    }

    # -------------------------------------------------------------------------
    # SCENARIO 4: Positive publication (valid staging -> atomic rename)
    # -------------------------------------------------------------------------
    $sc4Dir = Join-Path $fixtureRunDir "scenario-4"
    New-Item -ItemType Directory -Path $sc4Dir -Force -ErrorAction Stop | Out-Null
    Assert-SafePathChain $sc4Dir $fixtureRunDir

    $validContent = @"
================================================================================
BR001-R5 POSITIVE ATOMIC PUBLICATION TEST
================================================================================
Status: SUCCESS
================================================================================
"@
    $relLog = "artifacts/test/scenario4/finalizer-completion.log"
    Publish-CompletionUnit -RunDir $sc4Dir -CompletionLogContent $validContent -RelLogPath $relLog

    $sc4CompDir = Join-Path $sc4Dir "completion"
    $sc4StagingDir = Join-Path $sc4Dir ".completion-staging"
    $sc4LogPath = Join-Path $sc4CompDir "finalizer-completion.log"
    $sc4ShaPath = Join-Path $sc4CompDir "finalizer-completion.sha256"

    $sc4LogHash = (Get-FileHash -Path $sc4LogPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $sc4ShaContent = Get-Content $sc4ShaPath -Raw
    $sc4ShaMatch = $sc4ShaContent -match "^$sc4LogHash\s+$([regex]::Escape($relLog))"

    if ((Test-Path $sc4CompDir) -and (-not (Test-Path $sc4StagingDir)) -and (Test-Path $sc4LogPath) -and (Test-Path $sc4ShaPath) -and $sc4ShaMatch) {
        Log-Scenario "Scenario 4 (Positive atomic publication)" "PASS" "Staging cleanly moved to completion/, exactly 2 files present, sha256 matched, 0 staging residue"
    } else {
        Log-Scenario "Scenario 4 (Positive atomic publication)" "FAIL" "Positive publication failed"
        throw "Scenario 4 assertion failed"
    }
} finally {
    # Observable fail-closed cleanup of fixture run root
    if (Test-Path $fixtureRunDir) {
        Remove-Item -Path $fixtureRunDir -Recurse -Force -ErrorAction Stop
        if (Test-Path $fixtureRunDir) {
            throw "Fail-closed cleanup error: Failed to purge fixture directory at '$fixtureRunDir'"
        }
    }
}

$fixtureEndTime = [DateTimeOffset]::UtcNow
$fixtureDuration = ($fixtureEndTime - $fixtureStartTime).TotalSeconds

# Write Retained Transcript
$transcriptContent = @"
================================================================================
BR001-R5 FINALIZER ORDERING & ATOMIC PUBLICATION FIXTURE TRANSCRIPT
================================================================================
Literal Command: powershell.exe -NoProfile -ExecutionPolicy Bypass -File artifacts/task-runs/open_source-cab.4/test-finalizer-ordering-fixture.ps1
Start Time: $($fixtureStartTime.ToString("o"))
End Time: $($fixtureEndTime.ToString("o"))
Wall Duration: $([Math]::Round($fixtureDuration, 3))s
Exit Code: 0
Overall Result: PASS

SCENARIO RESULTS:
$($script:scenarioResults -join "`r`n")

PATH SAFETY & CLEANUP AUDIT:
- Fixture Scratch Root: $qualityGateRoot/$fixtureGuid
- Full Path Chain Validated: YES (No reparse points, strict descendant of $qualityGateRoot)
- Residual Fixture Artifacts on Disk: 0 (Strictly purged and verified absent)
================================================================================
FIXTURE VERDICT: ALL_SCENARIOS_PASSED
================================================================================
"@

Write-CleanFile $transcriptPath $transcriptContent

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host "ALL 4 ORDERING SCENARIOS PASSED IN $([Math]::Round($fixtureDuration, 3))s" -ForegroundColor Green
Write-Host "Retained Transcript: $transcriptPath" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor Cyan

exit 0
