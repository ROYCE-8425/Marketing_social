[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$evidenceDir = $PSScriptRoot
$gitRoot = (Get-Item $evidenceDir).Parent.Parent.Parent.FullName

# 1. Generate verification-output.sha256
$lines = [System.Collections.Generic.List[string]]::new()

# Raw logs
$rawLogsDir = Join-Path $evidenceDir "raw-logs"
if (Test-Path $rawLogsDir) {
    $rawLogs = Get-ChildItem -Path $rawLogsDir -File | Sort-Object Name
    foreach ($f in $rawLogs) {
        $h = (Get-FileHash $f.FullName -Algorithm SHA256).Hash.ToLower()
        $rel = "raw-logs/" + $f.Name
        $lines.Add("$h  $rel")
    }
}

# Task run artifacts
$taskRunFiles = @(
    "verification-matrix.md",
    "run-r4-verification.ps1",
    "test-safety-contracts.ps1",
    "safety-test-results.json",
    "safety-transcript.log",
    "docker-pre-inventory.json",
    "docker-post-inventory.json",
    "docker-comparison.json"
)
foreach ($tf in $taskRunFiles) {
    $p = Join-Path $evidenceDir $tf
    if (Test-Path $p) {
        $h = (Get-FileHash $p -Algorithm SHA256).Hash.ToLower()
        $lines.Add("$h  $tf")
    }
}

# Root and script deliverables
$rootDeliverables = @(
    "compose.yaml",
    "Dockerfile",
    "NuGet.Config",
    "Directory.Packages.props",
    "DXOS.slnx",
    "scripts/smoke-runtime.ps1",
    "scripts/check.ps1",
    "scripts/check-contract.json",
    "scripts/verify-check-contract.ps1",
    "src/DXOS.Api/Program.cs",
    "src/DXOS.AppHost/Program.cs"
)
foreach ($rd in $rootDeliverables) {
    $p = Join-Path $gitRoot $rd
    if (Test-Path $p) {
        $h = (Get-FileHash $p -Algorithm SHA256).Hash.ToLower()
        $lines.Add("$h  $rd")
    }
}

# 9 lock files
$lockFiles = Get-ChildItem -Path (Join-Path $gitRoot "src"), (Join-Path $gitRoot "tests") -Filter "packages.lock.json" -Recurse | Sort-Object FullName
foreach ($lf in $lockFiles) {
    $h = (Get-FileHash $lf.FullName -Algorithm SHA256).Hash.ToLower()
    $rel = $lf.FullName.Substring($gitRoot.Length + 1).Replace('\', '/')
    $lines.Add("$h  $rel")
}

$vOutPath = Join-Path $evidenceDir "verification-output.sha256"
[System.IO.File]::WriteAllText($vOutPath, ($lines -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "Generated $vOutPath with $($lines.Count) entries."

# 2. Generate reports.sha256
$reportFiles = @(
    "prompt.md",
    "implementation-report.md",
    "verification.md",
    "package-and-license-delta.md",
    "verification-output.sha256"
)
$rLines = [System.Collections.Generic.List[string]]::new()
foreach ($rf in $reportFiles) {
    $p = Join-Path $evidenceDir $rf
    if (Test-Path $p) {
        $h = (Get-FileHash $p -Algorithm SHA256).Hash.ToLower()
        $rLines.Add("$h  $rf")
    }
}
$rPath = Join-Path $evidenceDir "reports.sha256"
[System.IO.File]::WriteAllText($rPath, ($rLines -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "Generated $rPath with $($rLines.Count) entries."

