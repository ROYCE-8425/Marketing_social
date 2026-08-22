param(
    [Parameter(Mandatory = $true)]
    [string]$ProjectPath,

    [Parameter(Mandatory = $false)]
    [int]$TimeoutSeconds = 120,

    [Parameter(Mandatory = $false)]
    [string]$ResultPath = "",

    [Parameter(Mandatory = $false)]
    [string]$EvidenceRoot = ""
)

$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

if (-not (Test-Path ".git")) {
    [Console]::Error.WriteLine("run-test-project.ps1 must be executed from the repository root")
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

# Whitelist approved test projects
$approvedProjects = @(
    "tests/DXOS.Unit.Tests/DXOS.Unit.Tests.csproj",
    "tests/DXOS.Architecture.Tests/DXOS.Architecture.Tests.csproj",
    "tests/DXOS.Integration.Tests/DXOS.Integration.Tests.csproj",
    "tests\DXOS.Unit.Tests\DXOS.Unit.Tests.csproj",
    "tests\DXOS.Architecture.Tests\DXOS.Architecture.Tests.csproj",
    "tests\DXOS.Integration.Tests\DXOS.Integration.Tests.csproj"
)

$normalizedInputProj = $ProjectPath.Replace('\', '/')
$isApproved = $false
foreach ($ap in $approvedProjects) {
    if ($normalizedInputProj.Equals($ap.Replace('\', '/'), [System.StringComparison]::OrdinalIgnoreCase)) {
        $isApproved = $true
        break
    }
}

if (-not $isApproved) {
    [Console]::Error.WriteLine("Error: Project path '$ProjectPath' is not in the approved test project whitelist.")
    exit 1
}

$fullProjectPath = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($expectedRoot, $ProjectPath))
[void](Assert-SafePathChain $fullProjectPath $expectedRoot)

if (-not (Test-Path $fullProjectPath)) {
    [Console]::Error.WriteLine("Error: Project file does not exist: $fullProjectPath")
    exit 1
}

$projectDir = [System.IO.Path]::GetDirectoryName($fullProjectPath)
$binTestResultsDir = [System.IO.Path]::Combine($projectDir, "bin", "Release", "net10.0", "TestResults")

$reportGuid = [Guid]::NewGuid().ToString("N")
$ctrfReportName = "ctrf-report-${reportGuid}.json"

$effectiveEvidenceRoot = if ([string]::IsNullOrWhiteSpace($EvidenceRoot)) {
    [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($expectedRoot, "artifacts", "quality-gate"))
} else {
    if ([System.IO.Path]::IsPathRooted($EvidenceRoot)) {
        [System.IO.Path]::GetFullPath($EvidenceRoot)
    } else {
        [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($expectedRoot, $EvidenceRoot))
    }
}

# EvidenceRoot must be within artifacts/quality-gate or artifacts/task-runs
$allowedEvidenceBaseRoots = @(
    [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($expectedRoot, "artifacts", "quality-gate")),
    [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($expectedRoot, "artifacts", "task-runs"))
)
$isEvidenceRootAllowed = $false
foreach ($baseRoot in $allowedEvidenceBaseRoots) {
    $baseWithSep = $baseRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if ($effectiveEvidenceRoot.StartsWith($baseWithSep, [System.StringComparison]::OrdinalIgnoreCase) -or $effectiveEvidenceRoot.Equals($baseRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
        $isEvidenceRootAllowed = $true
        break
    }
}
if (-not $isEvidenceRootAllowed) {
    [Console]::Error.WriteLine("Security violation: EvidenceRoot '$effectiveEvidenceRoot' is not within approved evidence trees.")
    exit 1
}

$fullResultDestination = $null
if (-not [string]::IsNullOrWhiteSpace($ResultPath)) {
    if ([System.IO.Path]::IsPathRooted($ResultPath)) {
        [Console]::Error.WriteLine("Error: ResultPath '$ResultPath' must not be rooted.")
        exit 1
    }
    if ($ResultPath -match '\.\.') {
        [Console]::Error.WriteLine("Error: ResultPath '$ResultPath' must not contain '..'.")
        exit 1
    }

    $candidateDest = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($expectedRoot, $ResultPath))

    # Must be a strict descendant of the effective EvidenceRoot
    $effectiveEvidenceRootWithSep = $effectiveEvidenceRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $candidateDest.StartsWith($effectiveEvidenceRootWithSep, [System.StringComparison]::OrdinalIgnoreCase)) {
        [Console]::Error.WriteLine("Security violation: ResultPath '$ResultPath' ($candidateDest) must be a strict descendant of effective EvidenceRoot '$effectiveEvidenceRoot'.")
        exit 1
    }

    # Validate existing ancestor chain before creating missing directories
    $destDir = [System.IO.Path]::GetDirectoryName($candidateDest)
    $existingAncestor = $destDir
    while (-not (Test-Path $existingAncestor) -and -not [string]::IsNullOrEmpty($existingAncestor)) {
        $existingAncestor = [System.IO.Path]::GetDirectoryName($existingAncestor)
    }
    if (-not [string]::IsNullOrEmpty($existingAncestor)) {
        [void](Assert-SafePathChain $existingAncestor $expectedRoot)
    }

    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    [void](Assert-SafePathChain $destDir $expectedRoot)
    $fullResultDestination = $candidateDest
}

# Invoke official .NET 10 MTP dotnet test workflow
$procArgs = "test `"$fullProjectPath`" -c Release --no-build --no-restore -- --report-ctrf --report-ctrf-filename `"$ctrfReportName`""
Write-Host "Executing test runner: dotnet $procArgs"

$isWin = $IsWindows -or ($env:OS -eq "Windows_NT")
$startParams = @{
    FilePath = "dotnet"
    ArgumentList = $procArgs
    PassThru = $true
}
if ($isWin) {
    $startParams["WindowStyle"] = "Hidden"
}
$proc = Start-Process @startParams

$timedOut = $false
try {
    $proc | Wait-Process -Timeout $TimeoutSeconds -ErrorAction Stop
} catch {
    $timedOut = $true
    Write-Host "Test execution timed out after $TimeoutSeconds seconds." -ForegroundColor Red
    if ($null -ne $proc -and -not $proc.HasExited) {
        if ($isWin) {
            try {
                $killProc = Start-Process -FilePath "taskkill.exe" -ArgumentList "/T", "/F", "/PID", "$($proc.Id)" -WindowStyle Hidden -PassThru -ErrorAction SilentlyContinue
                if ($null -ne $killProc) {
                    try { [void]$killProc.WaitForExit(10000) } catch {}
                    $killProc.Dispose()
                }
            } catch {}
        }
        if (-not $proc.HasExited) {
            $proc | Stop-Process -Force -ErrorAction SilentlyContinue
        }
    }
}

$exitCode = if ($timedOut) { -1 } else { $proc.ExitCode }
if ($null -ne $proc) { $proc.Dispose() }

if ($timedOut) {
    [Console]::Error.WriteLine("Error: Test execution timed out.")
    exit 1
}

# Locate generated CTRF report
$generatedReportPath = [System.IO.Path]::Combine($binTestResultsDir, $ctrfReportName)
if (-not (Test-Path $generatedReportPath)) {
    [Console]::Error.WriteLine("Error: Test result report was not produced at expected path: $generatedReportPath")
    if ($exitCode -ne 0) {
        exit $exitCode
    }
    exit 1
}

# Copy to requested destination if specified
if ($null -ne $fullResultDestination) {
    Copy-Item -Path $generatedReportPath -Destination $fullResultDestination -Force
}

# Validate report contents
try {
    $rawJson = [System.IO.File]::ReadAllText($generatedReportPath)
    $reportObj = $rawJson | ConvertFrom-Json
} catch {
    [Console]::Error.WriteLine("Error: Malformed test report JSON: $_")
    exit 1
}

if ($null -eq $reportObj.results -or $null -eq $reportObj.results.summary) {
    [Console]::Error.WriteLine("Error: Missing 'results.summary' section in test report.")
    exit 1
}

$summary = $reportObj.results.summary
$totalTests = $summary.tests
$passed = $summary.passed
$failed = $summary.failed
$skipped = $summary.skipped

Write-Host "Test Results Summary: Total=$totalTests, Passed=$passed, Failed=$failed, Skipped=$skipped"

if ($totalTests -le 0) {
    [Console]::Error.WriteLine("Error: Zero tests were executed ($totalTests). Non-zero test count is required.")
    exit 1
}

if ($failed -gt 0) {
    [Console]::Error.WriteLine("Error: $failed test(s) failed.")
    if ($exitCode -eq 0) { exit 1 }
    exit $exitCode
}

if ($skipped -gt 0) {
    [Console]::Error.WriteLine("Error: $skipped test(s) skipped. Skipped tests are not allowed in required suites.")
    exit 1
}

if ($exitCode -ne 0) {
    [Console]::Error.WriteLine("Error: Test runner exited with non-zero code $exitCode")
    exit $exitCode
}

Write-Host "Test project passed successfully." -ForegroundColor Green
exit 0
