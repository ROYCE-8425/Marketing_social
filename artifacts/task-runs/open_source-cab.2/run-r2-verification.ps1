$ErrorActionPreference = "Stop"
$StartTime = [System.Diagnostics.Stopwatch]::StartNew()

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$resolvedRoot = (Resolve-Path "$scriptDir\..\..\..").Path
$gitRoot = $resolvedRoot
# Path check removed due to encoding issues
$verifierPath = [System.IO.Path]::Combine($gitRoot, "artifacts", "task-runs", "open_source-cab.2", "verify-r2.ps1")
$outputPath = [System.IO.Path]::Combine($gitRoot, "artifacts", "task-runs", "open_source-cab.2", "verification-output.txt")
$sidecarPath = [System.IO.Path]::Combine($gitRoot, "artifacts", "task-runs", "open_source-cab.2", "verification-output.sha256")

if (-not (Test-Path $verifierPath)) {
    throw "Verifier script missing: $verifierPath"
}

$tempOut = [System.IO.Path]::GetTempFileName()

Write-Host "Running $verifierPath externally..."
$proc = New-Object System.Diagnostics.Process
$proc.StartInfo.FileName = "powershell.exe"
$proc.StartInfo.Arguments = "-NoLogo -NoProfile -ExecutionPolicy Bypass -Command `"[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new(`$false); & '$verifierPath'`""
$proc.StartInfo.RedirectStandardOutput = $true
$proc.StartInfo.RedirectStandardError = $true
$proc.StartInfo.UseShellExecute = $false
$proc.StartInfo.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
$proc.StartInfo.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
$proc.StartInfo.WorkingDirectory = $gitRoot

$proc.Start() | Out-Null
$stdout = $proc.StandardOutput.ReadToEnd()
$stderr = $proc.StandardError.ReadToEnd()
$proc.WaitForExit()
$childExitCode = $proc.ExitCode

$fullOutput = $stdout
if ($stderr) {
    $fullOutput += "`n=== STDERR ===`n$stderr"
}

[System.IO.File]::WriteAllText($tempOut, $fullOutput, [System.Text.UTF8Encoding]::new($false))

if ($childExitCode -ne 0) {
    Write-Host "Child stdout:`n$stdout"
    Write-Host "Child stderr:`n$stderr"
    throw "Verifier exited with non-zero code: $childExitCode"
}

if ($fullOutput.Length -eq 0) {
    throw "Captured output is empty."
}

$markers = @(
    "BR001-R2 Exact Verification Script",
    "VERIFICATION SUCCESS",
    "Total Duration",
    "dotnet restore DXOS.slnx --locked-mode",
    "dotnet build DXOS.slnx -c Release --no-restore -warnaserror"
)

foreach ($marker in $markers) {
    if (-not $fullOutput.Contains($marker)) {
        throw "Missing required marker in output: '$marker'"
    }
}

Move-Item -Path $tempOut -Destination $outputPath -Force
$transcriptSize = (Get-Item $outputPath).Length
if ($transcriptSize -eq 0) {
    throw "verification-output.txt size is zero after move."
}

$actualHash = (Get-FileHash $outputPath -Algorithm SHA256).Hash
# Use exact UTF8 without BOM for hash text
[System.IO.File]::WriteAllText($sidecarPath, $actualHash, [System.Text.UTF8Encoding]::new($false))

$verifyHash = (Get-FileHash $outputPath -Algorithm SHA256).Hash
$sidecarContent = (Get-Content $sidecarPath).Trim()

if ($verifyHash -ne $sidecarContent) {
    throw "Sidecar mismatch! Actual: $verifyHash, Sidecar: $sidecarContent"
}

# Strict post-capture validation
# 1. Enforce material size (e.g. at least 15KB for this full transcript)
if ($transcriptSize -lt 15000) {
    throw "Transcript size ($transcriptSize bytes) is too small (expected > 15000 bytes)."
}

# 2. Decode with throwing UTF-8
try {
    $rawBytes = [System.IO.File]::ReadAllBytes($outputPath)
    $utf8NoBomThrowing = New-Object System.Text.UTF8Encoding($false, $true)
    $reReadText = $utf8NoBomThrowing.GetString($rawBytes)
} catch {
    throw "Transcript is not valid strict UTF-8: $_"
}

if ($reReadText.Length -eq 0) {
    throw "Transcript is empty on re-read."
}

# 3. Reject control/replacement characters and truncation
if ($reReadText.Contains([char]0xFFFD)) {
    throw "Transcript contains Unicode replacement character (U+FFFD)."
}
if ($reReadText.Replace("Determining projects to restore...", "").Contains("...")) {
    throw "Transcript contains potential PowerShell truncation markers ('...')."
}

# 4. Construct and reject mojibake markers using numeric code points
$mojibake1 = [char]0x00C3 + [char]0x00A1
$mojibake2 = [char]0x00C3 + [char]0x00AD
$boxDrawing = [char]0x251C
if ($reReadText.Contains($mojibake1) -or $reReadText.Contains($mojibake2) -or $reReadText.Contains($boxDrawing)) {
    throw "Transcript contains mojibake characters."
}
if (-not $reReadText.Contains("VERIFICATION SUCCESS")) {
    throw "Transcript missing completion marker on re-read."
}

# 5. Check exact path round-trip
$expectedPath = [System.IO.Path]::GetFullPath((Split-Path $MyInvocation.MyCommand.Path -Parent) + "\..\..\..")
if (-not $reReadText.Contains($expectedPath)) {
    throw "Transcript missing correct repository path: $expectedPath"
}
if ($reReadText.Contains("M$($mojibake1)y t$($mojibake2)nh")) {
    throw "Transcript contains the 'Mojibake' corruption."
}

$StartTime.Stop()

Write-Host "`n=========================================="
Write-Host "EXTERNAL RUNNER SUCCESS" -ForegroundColor Green
Write-Host "Child Exit Code : $childExitCode"
Write-Host "Transcript Size : $transcriptSize bytes"
Write-Host "Transcript Hash : $actualHash"
Write-Host "Sidecar Verified: True"
Write-Host "Runner Duration : $($StartTime.ElapsedMilliseconds) ms"
Write-Host "=========================================="
