[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item (Join-Path $PSScriptRoot "..")).FullName
$contractPath = Join-Path $PSScriptRoot "check-contract.json"
$checkScriptPath = Join-Path $PSScriptRoot "check.ps1"
$pwshExe = (Get-Process -Id $PID).Path
$utf8 = [System.Text.UTF8Encoding]::new($false)

function ConvertTo-ProcessArgument {
    param([string]$Value)
    if ($null -eq $Value) { return '""' }
    if ($Value -match '[\s"]') {
        return '"' + ($Value -replace '"', '\"') + '"'
    }
    return $Value
}

function Invoke-PwshFile {
    param(
        [string]$File,
        [string[]]$Arguments
    )
    $quoted = @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", (ConvertTo-ProcessArgument $File))
    foreach ($a in $Arguments) {
        $quoted += (ConvertTo-ProcessArgument $a)
    }
    $proc = Start-Process -FilePath $pwshExe -ArgumentList $quoted -PassThru -Wait -NoNewWindow
    return $proc.ExitCode
}

function Write-TestSidecar {
    param(
        [string]$ArtifactPath,
        [string]$RunId,
        [string]$Scanner
    )
    $hash = (Get-FileHash -Path $ArtifactPath -Algorithm SHA256).Hash.ToUpperInvariant()
    $meta = @{
        runId = $RunId
        scanner = $Scanner
        artifact = [System.IO.Path]::GetFileName($ArtifactPath)
        sha256 = $hash
        generatedAt = (Get-Date).ToString("o")
    }
    [System.IO.File]::WriteAllText("$ArtifactPath.meta.json", ($meta | ConvertTo-Json -Compress), $utf8)
}

function New-CleanScanFixture {
    param(
        [string]$Dir,
        [string]$RunId
    )
    [System.IO.File]::WriteAllText((Join-Path $Dir "gitleaks-report.json"), "[]", $utf8)
    [System.IO.File]::WriteAllText((Join-Path $Dir "trivy-config-report.json"), '{"Results":[]}', $utf8)
    [System.IO.File]::WriteAllText((Join-Path $Dir "trivy-image-report.json"), '{"Results":[]}', $utf8)
    [System.IO.File]::WriteAllText((Join-Path $Dir "grype-report.json"), '{"matches":[]}', $utf8)
    $prodSbom = Join-Path $repoRoot "artifacts\sbom.cdx.json"
    Copy-Item -Path $prodSbom -Destination (Join-Path $Dir "sbom.cdx.json") -Force
    Write-TestSidecar (Join-Path $Dir "gitleaks-report.json") $RunId "gitleaks"
    Write-TestSidecar (Join-Path $Dir "trivy-config-report.json") $RunId "trivy-config"
    Write-TestSidecar (Join-Path $Dir "trivy-image-report.json") $RunId "trivy-image"
    Write-TestSidecar (Join-Path $Dir "grype-report.json") $RunId "grype"
    Write-TestSidecar (Join-Path $Dir "sbom.cdx.json") $RunId "syft"
}

Write-Host "Running Comprehensive Verifier Assertions with Real Negative Fixtures..." -ForegroundColor Cyan

# -------------------------------------------------------------
# Assertion 1: Missing tool preflight check
# -------------------------------------------------------------
$testGuid1 = [guid]::NewGuid().ToString()
$fakeContractPath = Join-Path $PSScriptRoot "check-contract-test-$testGuid1.json"

try {
    $contractObj = Get-Content $contractPath -Raw | ConvertFrom-Json
    $fakeGateId = "fake-tool-gate-$testGuid1"
    $fakeToolName = "nonexistent-tool-$testGuid1"

    $fakeGate = [PSCustomObject]@{
        id = $fakeGateId
        task = "BR001-R3.1"
        profiles = @("TestProfile")
        status = "READY"
        command = $fakeToolName
        arguments = @()
        timeoutSeconds = 5
        required = $true
        activation = "always"
        activationCondition = "Test missing tool failure"
        requiredTools = @($fakeToolName)
        requiredFiles = @()
        expectedOutputs = @()
        failureSemantics = "fail-fast"
    }

    $subsequentGate = [PSCustomObject]@{
        id = "subsequent-gate-$testGuid1"
        task = "BR001-R3.1"
        profiles = @("TestProfile")
        status = "READY"
        command = "powershell"
        arguments = @("-Command", "Write-Host 'SHOULD NOT RUN'")
        timeoutSeconds = 5
        required = $true
        activation = "always"
        activationCondition = "Subsequent gate"
        requiredTools = @("powershell")
        requiredFiles = @()
        expectedOutputs = @()
        failureSemantics = "fail-fast"
    }

    $contractObj.profiles | Add-Member -NotePropertyName "TestProfile" -NotePropertyValue "Test Profile" -Force
    $contractObj.gates += $fakeGate
    $contractObj.gates += $subsequentGate
    [System.IO.File]::WriteAllText($fakeContractPath, ($contractObj | ConvertTo-Json -Depth 10), $utf8)

    $fakeContractRel = "scripts\check-contract-test-$testGuid1.json"
    $exitCode = Invoke-PwshFile -File $checkScriptPath -Arguments @("-Profile", "TestProfile", "-ContractPath", $fakeContractRel)
    if ($exitCode -eq 0) {
        throw "Assertion 1 Failed: check.ps1 should have failed on missing tool, but returned exit code 0"
    }

    $evidenceFiles = Get-ChildItem -Path (Join-Path $repoRoot "artifacts/quality-gate") -Recurse -Filter "evidence-TestProfile-*.json" | Sort-Object LastWriteTime -Descending
    if ($evidenceFiles.Count -eq 0) {
        throw "Assertion 1 Failed: Machine-readable evidence file was not created on preflight failure"
    }

    $latestEvidence = Get-Content $evidenceFiles[0].FullName -Raw | ConvertFrom-Json
    if ($latestEvidence.overallResult -ne "FAIL") {
        throw "Assertion 1 Failed: Evidence overallResult should be 'FAIL', was '$($latestEvidence.overallResult)'"
    }
    if ($latestEvidence.firstFailure -ne $fakeGateId) {
        throw "Assertion 1 Failed: Evidence firstFailure should be '$fakeGateId', was '$($latestEvidence.firstFailure)'"
    }
    if ($latestEvidence.gates.Count -ne 1 -or $latestEvidence.gates[0].id -ne $fakeGateId) {
        throw "Assertion 1 Failed: Preflight check did not block subsequent gates from executing"
    }

    Write-Host "[PASS] Missing tool preflight: non-zero exit, machine-readable evidence, blocked subsequent gates" -ForegroundColor Green
} finally {
    if (Test-Path $fakeContractPath) { Remove-Item $fakeContractPath -Force }
    Get-ChildItem -Path (Join-Path $repoRoot "artifacts/quality-gate") -Recurse -Filter "*TestProfile*" | Remove-Item -Force -Recurse
}

# -------------------------------------------------------------
# Assertion 2: Security summary rejects clean-but-stale report hashes
# -------------------------------------------------------------
$testDir2 = Join-Path $repoRoot "artifacts\quality-gate\test-stale-hash-$([guid]::NewGuid().ToString('N'))"
$runId2 = [guid]::NewGuid().ToString()
[void](New-Item -ItemType Directory -Path $testDir2 -Force)
$secSummaryScript = Join-Path $PSScriptRoot "generate-security-summary.ps1"
$committedTemp = Join-Path $testDir2 "security-summary.json"

try {
    New-CleanScanFixture -Dir $testDir2 -RunId $runId2
    $exitClean = Invoke-PwshFile -File $secSummaryScript -Arguments @("-EvidenceDir", $testDir2, "-RunId", $runId2, "-CommittedSummaryPath", $committedTemp)
    if ($exitClean -ne 0) {
        throw "Assertion 2 Failed: generator should PASS on clean run-owned reports (exit $exitClean)"
    }

    $gitleaksPath = Join-Path $testDir2 "gitleaks-report.json"
    [System.IO.File]::WriteAllText($gitleaksPath, "[ ]", $utf8)
    Write-TestSidecar $gitleaksPath $runId2 "gitleaks"

    $exitStale = Invoke-PwshFile -File $secSummaryScript -Arguments @("-EvidenceDir", $testDir2, "-RunId", $runId2, "-CommittedSummaryPath", $committedTemp, "-ValidateExisting")
    if ($exitStale -eq 0) {
        throw "Assertion 2 Failed: ValidateExisting should reject a clean report whose hash no longer matches the recorded summary"
    }

    Write-Host "[PASS] Security summary ValidateExisting rejected clean-but-stale report hash" -ForegroundColor Green
} finally {
    if (Test-Path $testDir2) { Remove-Item $testDir2 -Recurse -Force }
}

# -------------------------------------------------------------
# Assertion 3: Real TAR safe-extraction against malicious archives
# -------------------------------------------------------------
$testTarDir = Join-Path $repoRoot "artifacts\quality-gate\test-tar-$([guid]::NewGuid().ToString('N'))"
[void](New-Item -ItemType Directory -Path $testTarDir -Force)
$setupScript = Join-Path $PSScriptRoot "setup-security-tools.ps1"
$stagingTarget = Join-Path $testTarDir "extracted"
[void](New-Item -ItemType Directory -Path $stagingTarget -Force)

try {
    $py = @'
import tarfile, io, sys
mode, path = sys.argv[1], sys.argv[2]
tf = tarfile.open(path, "w:gz")
if mode == "traversal":
    info = tarfile.TarInfo("../escaped.txt")
    data = b"x"
    info.size = len(data)
    tf.addfile(info, io.BytesIO(data))
elif mode == "absolute":
    info = tarfile.TarInfo("/tmp/abs.txt")
    data = b"x"
    info.size = len(data)
    tf.addfile(info, io.BytesIO(data))
elif mode == "symlink":
    info = tarfile.TarInfo("link")
    info.type = tarfile.SYMTYPE
    info.linkname = "../outside"
    tf.addfile(info)
elif mode == "clean":
    info = tarfile.TarInfo("valid.txt")
    data = b"ok"
    info.size = len(data)
    tf.addfile(info, io.BytesIO(data))
tf.close()
'@
    $pyFile = Join-Path $testTarDir "mktar.py"
    [System.IO.File]::WriteAllText($pyFile, $py, $utf8)

    $cleanTar = Join-Path $testTarDir "clean.tar.gz"
    python $pyFile clean $cleanTar
    $exitCleanTar = Invoke-PwshFile -File $setupScript -Arguments @("-ValidateTarArchive", $cleanTar, "-ValidateTarExtractTarget", $stagingTarget)
    if ($exitCleanTar -ne 0) {
        throw "Assertion 3 Failed: clean TAR should be accepted by Validate-TarArchiveSafely"
    }

    foreach ($mode in @("traversal", "absolute", "symlink")) {
        $badTar = Join-Path $testTarDir "$mode.tar.gz"
        python $pyFile $mode $badTar
        $outsideBefore = @(Get-ChildItem -Path $testTarDir -Force | Select-Object -ExpandProperty Name)
        $exitBad = Invoke-PwshFile -File $setupScript -Arguments @("-ValidateTarArchive", $badTar, "-ValidateTarExtractTarget", $stagingTarget)
        if ($exitBad -eq 0) {
            throw "Assertion 3 Failed: malicious TAR mode '$mode' was accepted"
        }
        $escaped = Join-Path $testTarDir "escaped.txt"
        if (Test-Path $escaped) {
            throw "Assertion 3 Failed: traversal TAR created an outside file"
        }
        $outsideAfter = @(Get-ChildItem -Path $testTarDir -Force | Select-Object -ExpandProperty Name)
        $newOutside = @($outsideAfter | Where-Object { $_ -notin $outsideBefore -and $_ -ne "$mode.tar.gz" })
        if ($newOutside.Count -gt 0) {
            throw "Assertion 3 Failed: malicious TAR '$mode' created unexpected outside entries: $($newOutside -join ', ')"
        }
    }

    Write-Host "[PASS] TAR validator accepted a clean archive and rejected traversal/absolute/symlink fixtures before extraction" -ForegroundColor Green
} finally {
    if (Test-Path $testTarDir) { Remove-Item $testTarDir -Recurse -Force }
}

# -------------------------------------------------------------
# Assertion 4: Real OSS negative fixtures calling verify-oss-compliance.ps1
# -------------------------------------------------------------
$testOssDir = Join-Path $repoRoot "artifacts\quality-gate\test-oss-$([guid]::NewGuid().ToString('N'))"
[void](New-Item -ItemType Directory -Path $testOssDir -Force)
$ossVerifyScript = Join-Path $PSScriptRoot "verify-oss-compliance.ps1"
$baseInv = Get-Content (Join-Path $repoRoot "artifacts\oss-inventory.json") -Raw | ConvertFrom-Json

try {
    $invMissing = $baseInv | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $invMissing.directPackages = @($invMissing.directPackages | Where-Object { $_.name -ne "Elsa" })
    $invMissingPath = Join-Path $testOssDir "inv-missing.json"
    [System.IO.File]::WriteAllText($invMissingPath, ($invMissing | ConvertTo-Json -Depth 20), $utf8)
    $proc1 = Invoke-PwshFile -File $ossVerifyScript -Arguments @("-Check", "Inventory", "-InventoryPath", $invMissingPath)
    if ($proc1 -eq 0) { throw "Assertion 4 Failed: missing package should fail verify-oss-compliance.ps1" }

    $invExtra = $baseInv | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $invExtra.directPackages += [PSCustomObject]@{ name = "Bogus.Extra.Package"; version = "1.0.0"; license = "MIT"; purpose = "test"; category = "direct" }
    $invExtraPath = Join-Path $testOssDir "inv-extra.json"
    [System.IO.File]::WriteAllText($invExtraPath, ($invExtra | ConvertTo-Json -Depth 20), $utf8)
    $proc2 = Invoke-PwshFile -File $ossVerifyScript -Arguments @("-Check", "Inventory", "-InventoryPath", $invExtraPath)
    if ($proc2 -eq 0) { throw "Assertion 4 Failed: extra package should fail verify-oss-compliance.ps1" }

    $invVer = $baseInv | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $elsaPkg = $invVer.directPackages | Where-Object { $_.name -eq "Elsa" } | Select-Object -First 1
    $elsaPkg.version = "9.9.9"
    $invVerPath = Join-Path $testOssDir "inv-wrong-ver.json"
    [System.IO.File]::WriteAllText($invVerPath, ($invVer | ConvertTo-Json -Depth 20), $utf8)
    $proc3 = Invoke-PwshFile -File $ossVerifyScript -Arguments @("-Check", "Inventory", "-InventoryPath", $invVerPath)
    if ($proc3 -eq 0) { throw "Assertion 4 Failed: wrong package version should fail verify-oss-compliance.ps1" }

    $invImg = $baseInv | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $pgImg = $invImg.containerImages | Where-Object { $_.name -eq "postgres" } | Select-Object -First 1
    $pgImg.digest = "sha256:0000000000000000000000000000000000000000000000000000000000000000"
    $invImgPath = Join-Path $testOssDir "inv-wrong-img.json"
    [System.IO.File]::WriteAllText($invImgPath, ($invImg | ConvertTo-Json -Depth 20), $utf8)
    $proc4 = Invoke-PwshFile -File $ossVerifyScript -Arguments @("-Check", "Inventory", "-InventoryPath", $invImgPath)
    if ($proc4 -eq 0) { throw "Assertion 4 Failed: wrong image digest should fail verify-oss-compliance.ps1" }

    $invExtraImg = $baseInv | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $invExtraImg.containerImages += [PSCustomObject]@{ name = "bogus.example/extra"; version = "1"; digest = "sha256:1111111111111111111111111111111111111111111111111111111111111111"; license = "MIT" }
    $invExtraImgPath = Join-Path $testOssDir "inv-extra-img.json"
    [System.IO.File]::WriteAllText($invExtraImgPath, ($invExtraImg | ConvertTo-Json -Depth 20), $utf8)
    $proc4b = Invoke-PwshFile -File $ossVerifyScript -Arguments @("-Check", "Inventory", "-InventoryPath", $invExtraImgPath)
    if ($proc4b -eq 0) { throw "Assertion 4 Failed: extra container image should fail verify-oss-compliance.ps1" }

    $invTool = $baseInv | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $invTool.securityTools = @($invTool.securityTools | Where-Object { $_.name -ne "trivy" })
    $invToolPath = Join-Path $testOssDir "inv-missing-tool.json"
    [System.IO.File]::WriteAllText($invToolPath, ($invTool | ConvertTo-Json -Depth 20), $utf8)
    $proc5 = Invoke-PwshFile -File $ossVerifyScript -Arguments @("-Check", "Inventory", "-InventoryPath", $invToolPath)
    if ($proc5 -eq 0) { throw "Assertion 4 Failed: missing security tool should fail verify-oss-compliance.ps1" }

    $invToolHash = $baseInv | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $trivyTool = $invToolHash.securityTools | Where-Object { $_.name -eq "trivy" } | Select-Object -First 1
    $trivyTool.windowsArchiveSha256 = "0000000000000000000000000000000000000000000000000000000000000000"
    $invToolHashPath = Join-Path $testOssDir "inv-wrong-tool-hash.json"
    [System.IO.File]::WriteAllText($invToolHashPath, ($invToolHash | ConvertTo-Json -Depth 20), $utf8)
    $proc5b = Invoke-PwshFile -File $ossVerifyScript -Arguments @("-Check", "Inventory", "-InventoryPath", $invToolHashPath)
    if ($proc5b -eq 0) { throw "Assertion 4 Failed: wrong security-tool acquisition hash should fail verify-oss-compliance.ps1" }

    $invSvc = $baseInv | ConvertTo-Json -Depth 20 | ConvertFrom-Json
    $invSvc.thirdPartyServices = @($invSvc.thirdPartyServices | Where-Object { $_.name -ne "Google Gemini" })
    $invSvcPath = Join-Path $testOssDir "inv-missing-svc.json"
    [System.IO.File]::WriteAllText($invSvcPath, ($invSvc | ConvertTo-Json -Depth 20), $utf8)
    $proc6 = Invoke-PwshFile -File $ossVerifyScript -Arguments @("-Check", "Inventory", "-InventoryPath", $invSvcPath)
    if ($proc6 -eq 0) { throw "Assertion 4 Failed: missing third-party service should fail verify-oss-compliance.ps1" }

    Write-Host "[PASS] Exact OSS reconciliation production-validator fixtures (missing/extra/wrong version/image digest/extra image/missing tool/wrong tool hash/missing service)" -ForegroundColor Green
} finally {
    if (Test-Path $testOssDir) { Remove-Item $testOssDir -Recurse -Force }
}

# -------------------------------------------------------------
# Assertion 5: SBOM duplicate detection via production summary validator
# -------------------------------------------------------------
$testSbomDir = Join-Path $repoRoot "artifacts\quality-gate\test-sbom-dup-$([guid]::NewGuid().ToString('N'))"
$runId5 = [guid]::NewGuid().ToString()
[void](New-Item -ItemType Directory -Path $testSbomDir -Force)
try {
    New-CleanScanFixture -Dir $testSbomDir -RunId $runId5
    $dupSbom = Get-Content (Join-Path $testSbomDir "sbom.cdx.json") -Raw | ConvertFrom-Json
    $dupSbom.components += $dupSbom.components[0]
    [System.IO.File]::WriteAllText((Join-Path $testSbomDir "sbom.cdx.json"), ($dupSbom | ConvertTo-Json -Depth 20), $utf8)
    Write-TestSidecar (Join-Path $testSbomDir "sbom.cdx.json") $runId5 "syft"
    $exitDup = Invoke-PwshFile -File $secSummaryScript -Arguments @("-EvidenceDir", $testSbomDir, "-RunId", $runId5, "-CommittedSummaryPath", (Join-Path $testSbomDir "security-summary.json"))
    if ($exitDup -eq 0) {
        throw "Assertion 5 Failed: generate-security-summary.ps1 should fail on duplicate canonical SBOM identities"
    }
    Write-Host "[PASS] Production SBOM validator rejected duplicate canonical component identities" -ForegroundColor Green
} finally {
    if (Test-Path $testSbomDir) { Remove-Item $testSbomDir -Recurse -Force }
}

# -------------------------------------------------------------
# Assertion 6: Full vs ReadyAudit gate counts and profile isolation
# -------------------------------------------------------------
$contractObj = Get-Content $contractPath -Raw | ConvertFrom-Json
$fullGates = $contractObj.gates | Where-Object { $_.profiles -contains "Full" }
$readyAuditGates = $contractObj.gates | Where-Object { $_.profiles -contains "ReadyAudit" }

$r8InFull = $fullGates | Where-Object { $_.task -match "^BR001-R8" }
if ($r8InFull.Count -ne 0) {
    throw "Assertion 6 Failed: Full profile must contain ZERO R8 gates, found $($r8InFull.Count)"
}
if ($fullGates.Count -ne 26) {
    throw "Assertion 6 Failed: Full profile should have exactly 26 gates, found $($fullGates.Count)"
}
if ($readyAuditGates.Count -ne 29) {
    throw "Assertion 6 Failed: ReadyAudit profile should have exactly 29 gates, found $($readyAuditGates.Count)"
}
$r8InReadyAudit = $readyAuditGates | Where-Object { $_.task -match "^BR001-R8" }
if ($r8InReadyAudit.Count -ne 3) {
    throw "Assertion 6 Failed: ReadyAudit profile should contain exactly 3 R8 gates, found $($r8InReadyAudit.Count)"
}
foreach ($g in $r8InReadyAudit) {
    if ($g.status -ne "NOT_IMPLEMENTED") {
        throw "Assertion 6 Failed: R8 gate $($g.id) must be NOT_IMPLEMENTED, was $($g.status)"
    }
}
Write-Host "[PASS] Full profile contains 0 R8 gates (26 gates); ReadyAudit contains 3 NOT_IMPLEMENTED R8 gates (29 gates)" -ForegroundColor Green

# -------------------------------------------------------------
# Assertion 7: NOT_IMPLEMENTED gate mechanics fail closed
# -------------------------------------------------------------
$testGuid7 = [guid]::NewGuid().ToString()
$fakeContract7 = Join-Path $PSScriptRoot "check-contract-test-$testGuid7.json"
try {
    $contractObj = Get-Content $contractPath -Raw | ConvertFrom-Json
    $fakeGateId7 = "fake-not-impl-gate-$testGuid7"
    $fakeGate7 = [PSCustomObject]@{
        id = $fakeGateId7
        task = "BR001-R8.1"
        profiles = @("TestNotImplProfile")
        status = "NOT_IMPLEMENTED"
        command = ""
        arguments = @()
        timeoutSeconds = 5
        required = $true
        activation = "always"
        activationCondition = "Test NOT_IMPLEMENTED fail-closed mechanics"
        requiredTools = @()
        requiredFiles = @()
        expectedOutputs = @()
        failureSemantics = "fail-fast"
    }
    $contractObj.profiles | Add-Member -NotePropertyName "TestNotImplProfile" -NotePropertyValue "Test NOT_IMPLEMENTED profile" -Force
    $contractObj.gates += $fakeGate7
    [System.IO.File]::WriteAllText($fakeContract7, ($contractObj | ConvertTo-Json -Depth 10), $utf8)
    $fakeContractRel7 = "scripts\check-contract-test-$testGuid7.json"
    $exitCode7 = Invoke-PwshFile -File $checkScriptPath -Arguments @("-Profile", "TestNotImplProfile", "-ContractPath", $fakeContractRel7)
    if ($exitCode7 -eq 0) {
        throw "Assertion 7 Failed: check.ps1 should have failed on NOT_IMPLEMENTED gate, returned exit code 0"
    }
    $evidence7 = Get-ChildItem -Path (Join-Path $repoRoot "artifacts/quality-gate") -Recurse -Filter "evidence-TestNotImplProfile-*.json" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if (-not $evidence7) {
        throw "Assertion 7 Failed: evidence file missing for TestNotImplProfile"
    }
    $ev7 = Get-Content $evidence7.FullName -Raw | ConvertFrom-Json
    if ($ev7.firstFailure -ne $fakeGateId7 -or $ev7.overallResult -ne "FAIL") {
        throw "Assertion 7 Failed: expected firstFailure '$fakeGateId7' and FAIL, got '$($ev7.firstFailure)' / '$($ev7.overallResult)'"
    }
    Write-Host "[PASS] NOT_IMPLEMENTED gate mechanics fail closed with exit code 1" -ForegroundColor Green
} finally {
    if (Test-Path $fakeContract7) { Remove-Item $fakeContract7 -Force }
    Get-ChildItem -Path (Join-Path $repoRoot "artifacts/quality-gate") -Recurse -Filter "*TestNotImplProfile*" | Remove-Item -Force -Recurse
}

Write-Host "`nAll verifier assertions with real production-validator fixtures passed safely!" -ForegroundColor Green
