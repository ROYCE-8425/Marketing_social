$ErrorActionPreference = "Stop"
$StartTime = Get-Date
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Ensure we're running from the root of DX-OS based on script location
$resolvedRoot = (Resolve-Path "$scriptDir\..\..\..").Path
$gitRoot = $resolvedRoot
if ($gitRoot -match "open_source$") {
    throw "Repository root cannot be 'open_source'"
}

Set-Location $gitRoot

function Run-Command {
    param(
        [string]$Command,
        [string[]]$Arguments,
        [switch]$ExpectFailure
    )
    $cmdString = "$Command " + ($Arguments -join " ")
    Write-Host "`n> $cmdString" -ForegroundColor Cyan
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    $proc = New-Object System.Diagnostics.Process
    $proc.StartInfo.FileName = $Command
    $proc.StartInfo.Arguments = $Arguments -join " "
    $proc.StartInfo.RedirectStandardOutput = $true
    $proc.StartInfo.RedirectStandardError = $true
    $proc.StartInfo.UseShellExecute = $false
    $proc.StartInfo.WorkingDirectory = $gitRoot
    $proc.Start() | Out-Null

    $stdout = $proc.StandardOutput.ReadToEnd()
    $stderr = $proc.StandardError.ReadToEnd()
    $proc.WaitForExit()
    $sw.Stop()

    $exitCode = $proc.ExitCode

    if ($stdout) { Write-Host $stdout }
    if ($stderr) { Write-Host $stderr -ForegroundColor Yellow }
    Write-Host "ExitCode: $exitCode, Duration: $($sw.ElapsedMilliseconds)ms" -ForegroundColor DarkGray

    if (-not $ExpectFailure -and $exitCode -ne 0) {
        throw "Command failed with exit code $exitCode`n$cmdString"
    }

    return @{
        Command = $cmdString;
        Stdout = $stdout;
        Stderr = $stderr;
        ExitCode = $exitCode;
        Duration = $sw.ElapsedMilliseconds
    }
}

Write-Host "=========================================="
Write-Host "BR001-R2 Exact Verification Script"
Write-Host "=========================================="

Write-Host "`n--- 0. Preflight Info & Hashes ---"
$null = Run-Command "dotnet" @("--info")
$null = Run-Command "git" @("rev-parse", "HEAD")
$null = Run-Command "git" @("branch", "--show-current")
$null = Run-Command "git" @("remote", "-v")

Write-Host "Hashes:"
$filesToHash = @(
    "NuGet.Config",
    "src\DXOS.Api\packages.lock.json",
    "src\DXOS.AppHost\packages.lock.json",
    "src\DXOS.Application\packages.lock.json",
    "src\DXOS.Domain\packages.lock.json",
    "src\DXOS.Infrastructure\packages.lock.json",
    "src\DXOS.Workflows\packages.lock.json",
    "tests\DXOS.Architecture.Tests\packages.lock.json",
    "tests\DXOS.Integration.Tests\packages.lock.json",
    "tests\DXOS.Unit.Tests\packages.lock.json"
)
foreach ($f in $filesToHash) {
    if (Test-Path $f) {
        $h = (Get-FileHash $f -Algorithm SHA256).Hash
        Write-Host "SHA256($f) = $h"
    } else {
        Write-Host "SHA256($f) = MISSING"
    }
}

Write-Host "`n--- 1. Exact SDK and Build Policy ---"
$sdkVer = Run-Command "dotnet" @("--version")
if ($sdkVer.Stdout.Trim() -ne "10.0.302") {
    throw "Expected .NET SDK 10.0.302, got $($sdkVer.Stdout.Trim())"
}

$globalJson = Get-Content "global.json" | ConvertFrom-Json
if ($globalJson.sdk.version -ne "10.0.302" -or $globalJson.sdk.rollForward -ne "latestPatch" -or $globalJson.sdk.allowPrerelease -ne $false) {
    throw "global.json policy violations found"
}

Write-Host "`nTesting unsupported solution property evaluation:"
$badMsbuild = Run-Command "dotnet" @("msbuild", "DXOS.slnx", "-getProperty:TargetFramework", "-getProperty:TreatWarningsAsErrors") -ExpectFailure
if ($badMsbuild.ExitCode -eq 0) {
    throw "Expected solution-level getProperty to fail, but it exited 0."
}

$expectedProjects = @(
    "src\DXOS.Api\DXOS.Api.csproj",
    "src\DXOS.AppHost\DXOS.AppHost.csproj",
    "src\DXOS.Application\DXOS.Application.csproj",
    "src\DXOS.Domain\DXOS.Domain.csproj",
    "src\DXOS.Infrastructure\DXOS.Infrastructure.csproj",
    "src\DXOS.Workflows\DXOS.Workflows.csproj",
    "tests\DXOS.Architecture.Tests\DXOS.Architecture.Tests.csproj",
    "tests\DXOS.Integration.Tests\DXOS.Integration.Tests.csproj",
    "tests\DXOS.Unit.Tests\DXOS.Unit.Tests.csproj"
)

foreach ($proj in $expectedProjects) {
    $prop = Run-Command "dotnet" @("msbuild", $proj, "-property:Configuration=Release", "-getProperty:TargetFramework", "-getProperty:TreatWarningsAsErrors")
    $propRaw = $prop.Stdout
    try {
        $json = $propRaw | ConvertFrom-Json
        $tf = $json.Properties.TargetFramework
        $twe = $json.Properties.TreatWarningsAsErrors
    } catch {
        throw "Failed to parse msbuild getProperty output as JSON: $propRaw"
    }

    if ($tf -ne "net10.0" -or $twe -ne "true") {
        throw "Project $proj properties incorrect: TargetFramework=$tf, TreatWarningsAsErrors=$twe"
    }

    $xml = [xml](Get-Content $proj)
    if (($xml.SelectNodes("//LangVersion") | Where-Object { $_.InnerText -match "preview|latest" }).Count -gt 0) {
        throw "Project $proj contains preview/latest LangVersion"
    }
    if ($xml.SelectNodes("//NoWarn").Count -gt 0) {
        throw "Project $proj contains NoWarn"
    }
    if ($xml.SelectNodes("//RepositoryUrl").Count -gt 0) {
        throw "Project $proj contains RepositoryUrl"
    }
}

Write-Host "`n--- 2. Exact Solution and Project Graph ---"
$slnListRaw = Run-Command "dotnet" @("sln", "DXOS.slnx", "list")
$slnProjects = $slnListRaw.Stdout -split "`n" | Where-Object { $_ -match "\.csproj" } | ForEach-Object { $_.Trim().Replace('/', [System.IO.Path]::DirectorySeparatorChar) }
$allCsprojs = (Get-ChildItem -Path $gitRoot -Filter *.csproj -Recurse | Select-Object -ExpandProperty FullName).Replace($gitRoot + [System.IO.Path]::DirectorySeparatorChar, "")

$expectedSorted = $expectedProjects | Sort-Object
$allSorted = $allCsprojs | Sort-Object
$slnSorted = $slnProjects | Sort-Object

if (($expectedSorted -join "|") -ne ($allSorted -join "|")) {
    throw "Mismatch between expected projects and actual csproj files:`nExpected: $($expectedSorted -join '|')`nActual: $($allSorted -join '|')"
}
if (($expectedSorted -join "|") -ne ($slnSorted -join "|")) {
    throw "Mismatch between expected projects and sln list:`nExpected: $($expectedSorted -join '|')`nActual: $($slnSorted -join '|')"
}

Write-Host "`nTesting unsupported solution reference list:"
$badRef = Run-Command "dotnet" @("list", "DXOS.slnx", "reference") -ExpectFailure
if ($badRef.ExitCode -eq 0) {
    throw "Expected solution-level reference list to fail, but it exited 0."
}

$expectedGraph = @{
    "src\DXOS.Domain\DXOS.Domain.csproj" = @()
    "src\DXOS.Application\DXOS.Application.csproj" = @("src\DXOS.Domain\DXOS.Domain.csproj")
    "src\DXOS.Workflows\DXOS.Workflows.csproj" = @("src\DXOS.Application\DXOS.Application.csproj")
    "src\DXOS.Infrastructure\DXOS.Infrastructure.csproj" = @("src\DXOS.Application\DXOS.Application.csproj", "src\DXOS.Domain\DXOS.Domain.csproj")
    "src\DXOS.Api\DXOS.Api.csproj" = @("src\DXOS.Application\DXOS.Application.csproj", "src\DXOS.Infrastructure\DXOS.Infrastructure.csproj", "src\DXOS.Workflows\DXOS.Workflows.csproj")
    "src\DXOS.AppHost\DXOS.AppHost.csproj" = @("src\DXOS.Api\DXOS.Api.csproj")
    "tests\DXOS.Architecture.Tests\DXOS.Architecture.Tests.csproj" = @(
        "src\DXOS.Api\DXOS.Api.csproj",
        "src\DXOS.Application\DXOS.Application.csproj",
        "src\DXOS.Domain\DXOS.Domain.csproj",
        "src\DXOS.Infrastructure\DXOS.Infrastructure.csproj",
        "src\DXOS.Workflows\DXOS.Workflows.csproj"
    )
    "tests\DXOS.Integration.Tests\DXOS.Integration.Tests.csproj" = @(
        "src\DXOS.Api\DXOS.Api.csproj",
        "src\DXOS.Application\DXOS.Application.csproj",
        "src\DXOS.Domain\DXOS.Domain.csproj",
        "src\DXOS.Infrastructure\DXOS.Infrastructure.csproj",
        "src\DXOS.Workflows\DXOS.Workflows.csproj"
    )
    "tests\DXOS.Unit.Tests\DXOS.Unit.Tests.csproj" = @(
        "src\DXOS.Api\DXOS.Api.csproj",
        "src\DXOS.Application\DXOS.Application.csproj",
        "src\DXOS.Domain\DXOS.Domain.csproj",
        "src\DXOS.Infrastructure\DXOS.Infrastructure.csproj",
        "src\DXOS.Workflows\DXOS.Workflows.csproj"
    )
}

foreach ($proj in $expectedProjects) {
    $refsRaw = Run-Command "dotnet" @("list", $proj, "reference")
    $projDir = Split-Path $proj -Parent

    $lines = $refsRaw.Stdout -split "`n" | Where-Object { $_.Trim() -match "^\.\.[\\/].*\.csproj$" } | ForEach-Object {
        $path = $_.Trim().Replace('/', [System.IO.Path]::DirectorySeparatorChar)
        if ($path -match "open_source|Elsa\.csproj|[A-Za-z]:[\\/]") {
            throw "Unauthorized absolute or external path found in references for $proj"
        }
        $abs = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($gitRoot, $projDir, $path))
        $abs.Replace($gitRoot + [System.IO.Path]::DirectorySeparatorChar, "")
    }

    # Resolve static XML to match
    $xml = [xml](Get-Content $proj)
    $staticRefs = @()
    if ($xml.Project.ItemGroup.ProjectReference) {
        $nodes = $xml.Project.ItemGroup.ProjectReference
        if ($nodes -isnot [array]) { $nodes = @($nodes) }
        foreach ($node in $nodes) {
            if ($null -eq $node) { continue }
            $path = $node.Include.Replace('/', [System.IO.Path]::DirectorySeparatorChar)
            if ($path -match "open_source|Elsa\.csproj|[A-Za-z]:[\\/]") {
                throw "Unauthorized absolute or external path found in static references for $proj"
            }
            $abs = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($gitRoot, $projDir, $path))
            $rel = $abs.Replace($gitRoot + [System.IO.Path]::DirectorySeparatorChar, "")
            $staticRefs += $rel
        }
    }

    $linesSorted = $lines | Sort-Object
    $staticSorted = $staticRefs | Sort-Object
    if (($linesSorted -join "|") -ne ($staticSorted -join "|")) {
        throw "Mismatch between dotnet list reference and static references in $proj`nDotnet: $($linesSorted -join '|')`nStatic: $($staticSorted -join '|')"
    }

    $expectedEdges = $expectedGraph[$proj] | Sort-Object
    if (($linesSorted -join "|") -ne ($expectedEdges -join "|")) {
        throw "Mismatch between actual and exact required edges for $proj`nExpected: $($expectedEdges -join '|')`nActual: $($linesSorted -join '|')"
    }

    $localVersions = $xml.SelectNodes("//PackageReference[@Version]")
    if ($localVersions.Count -gt 0) {
        throw "Project $proj contains per-project package versions"
    }
}

Write-Host "Exact project graph policy enforced. Acyclic property implicitly verified by edge map matching."
Write-Host "`n--- 3. CPM, Feeds, and Packages ---"
$nugetListRaw = Run-Command "dotnet" @("nuget", "list", "source")

$nugetXml = [xml](Get-Content "NuGet.Config")
$clearCount = $nugetXml.SelectNodes("//packageSources/clear").Count
if ($clearCount -ne 1) { throw "NuGet.Config must contain exactly one <clear /> in packageSources" }
$sources = $nugetXml.SelectNodes("//packageSources/add")
if ($sources.Count -ne 1) { throw "NuGet.Config must contain exactly one package source" }
if ($sources[0].key -ne "nuget.org") { throw "NuGet.Config source key must be exactly nuget.org" }
if ($sources[0].value -ne "https://api.nuget.org/v3/index.json") { throw "NuGet.Config source must be exactly https://api.nuget.org/v3/index.json" }

$lines = $nugetListRaw.Stdout -split "`n" | Where-Object { $_.Trim() }
$sourcesOutput = @()
foreach ($line in $lines) {
    $trimmed = $line.Trim()
    if ($trimmed -match "^\d+\.\s+(.*) \[(.*)\]$") {
        $sourcesOutput += @{ Name = $matches[1].Trim(); State = $matches[2].Trim(); Url = "" }
    } elseif ($trimmed -match "^https?://") {
        if ($sourcesOutput.Count -gt 0) {
            $sourcesOutput[$sourcesOutput.Count - 1].Url = $trimmed
        }
    }
}
if ($sourcesOutput.Count -ne 1) {
    throw "dotnet nuget list source must return exactly one source, found $($sourcesOutput.Count)"
}
if ($sourcesOutput[0].Name -ne "nuget.org" -or $sourcesOutput[0].State -ne "Enabled" -or $sourcesOutput[0].Url -ne "https://api.nuget.org/v3/index.json") {
    throw "dotnet nuget list source did not report exactly the nuget.org source enabled with v3 URL. Found: $($sourcesOutput[0].Name) [$($sourcesOutput[0].State)] $($sourcesOutput[0].Url)"
}

$dirPropsXml = [xml](Get-Content "Directory.Packages.props")
$cpm = $dirPropsXml.SelectNodes("//ManagePackageVersionsCentrally")
if ($cpm.Count -eq 0 -or $cpm[0].InnerText.ToLower() -ne "true") { throw "Directory.Packages.props must set ManagePackageVersionsCentrally to true" }
$locked = $dirPropsXml.SelectNodes("//RestorePackagesWithLockFile")
if ($locked.Count -eq 0 -or $locked[0].InnerText.ToLower() -ne "true") { throw "Directory.Packages.props must set RestorePackagesWithLockFile to true" }

$centralPackages = $dirPropsXml.SelectNodes("//PackageVersion")
if ($centralPackages.Count -ne 2) { throw "Directory.Packages.props must contain exactly 2 central packages" }
$foundXunit = $false
$foundTestSdk = $false
foreach ($pkg in $centralPackages) {
    $version = $pkg.Version
    if (-not $version) { throw "Central package missing Version attribute" }
    if ($version -match "-|preview|\*") { throw "Central package version cannot be floating or preview: $version" }
    if ($pkg.Include -eq "xunit.v3" -and $version -eq "3.2.2") { $foundXunit = $true }
    if ($pkg.Include -eq "Microsoft.NET.Test.Sdk" -and $version -eq "17.13.0") { $foundTestSdk = $true }
}
if (-not $foundXunit -or -not $foundTestSdk) { throw "Central packages must be exactly xunit.v3 3.2.2 and Microsoft.NET.Test.Sdk 17.13.0" }

foreach ($proj in $expectedProjects) {
    $xml = [xml](Get-Content $proj)
    $pkgRefs = $xml.SelectNodes("//PackageReference")
    if ($proj -match "tests[\\/]") {
        if ($pkgRefs.Count -ne 2) { throw "Test project $proj must have exactly 2 direct packages" }
        $hasXunit = $false
        $hasTestSdk = $false
        foreach ($pkg in $pkgRefs) {
            if ($pkg.Include -eq "xunit.v3") { $hasXunit = $true }
            if ($pkg.Include -eq "Microsoft.NET.Test.Sdk") { $hasTestSdk = $true }
        }
        if (-not $hasXunit -or -not $hasTestSdk) { throw "Test project $proj missing required packages" }
    } else {
        if ($pkgRefs.Count -gt 0) { throw "Production project $proj cannot have direct packages" }
    }
}

$packagesRaw = Run-Command "dotnet" @("list", "DXOS.slnx", "package", "--include-transitive")
if ($packagesRaw.Stdout -match "Elsa|Npgsql|Aspire|ArchUnit") {
    throw "Forbidden packages found in dependency graph"
}

Write-Host "`nPackage | Version | Consumers | License | Evidence Source"
Write-Host "------- | ------- | --------- | ------- | ---------------"

$uniquePackages = @{}
$expectedLocks = $expectedProjects | ForEach-Object { (Split-Path $_ -Parent) + "\packages.lock.json" }

foreach ($lock in $expectedLocks) {
    if (-not (Test-Path $lock)) { continue }
    $lockJson = Get-Content $lock -Raw | ConvertFrom-Json
    $projectName = (Split-Path (Split-Path $lock -Parent) -Leaf)

    foreach ($tf in $lockJson.dependencies.psobject.properties.name) {
        $deps = $lockJson.dependencies.$tf
        foreach ($pkg in $deps.psobject.properties.name) {
            $type = $deps.$pkg.type
            if ($type -eq "Project") { continue }
            $ver = $deps.$pkg.resolved
            $key = "$pkg|$ver"
            if (-not $uniquePackages.ContainsKey($key)) {
                $uniquePackages[$key] = @{
                    Name = $pkg;
                    Version = $ver;
                    Type = $type;
                    Consumers = @($projectName);
                }
            } else {
                if ($uniquePackages[$key].Consumers -notcontains $projectName) {
                    $uniquePackages[$key].Consumers += $projectName
                }
            }
        }
    }
}

$licenseCount = 0
foreach ($key in $uniquePackages.Keys | Sort-Object) {
    $pkg = $uniquePackages[$key]
    $nuspecPath = "$env:USERPROFILE\.nuget\packages\$($pkg.Name.ToLower())\$($pkg.Version)\$($pkg.Name.ToLower()).nuspec"
    if (-not (Test-Path $nuspecPath)) {
        throw "NuSpec not found for package $($pkg.Name) $($pkg.Version) at $nuspecPath"
    }
    $nuspecXml = [xml](Get-Content $nuspecPath)
    $licenseNodes = $nuspecXml.GetElementsByTagName("license")
    $licenseUrlNodes = $nuspecXml.GetElementsByTagName("licenseUrl")

    $license = ""
    $evidence = ""

    if ($licenseNodes.Count -gt 0 -and $licenseNodes[0].GetAttribute("type") -eq "expression") {
        $license = $licenseNodes[0].InnerText.Trim()
        $evidence = $nuspecPath
    } elseif ($licenseUrlNodes.Count -gt 0) {
        $license = $licenseUrlNodes[0].InnerText.Trim()
        $evidence = $nuspecPath
    } else {
        throw "No license expression or URL found for package $($pkg.Name) $($pkg.Version) in $nuspecPath"
    }

    $consumers = $pkg.Consumers -join ","
    Write-Host "$($pkg.Name) | $($pkg.Version) | $consumers | $license | $evidence"
    $licenseCount++
}

Write-Host ""
Write-Host "Unique Packages Count: $($uniquePackages.Count)"
Write-Host "Unique Licenses Count: $licenseCount"
if ($licenseCount -eq $uniquePackages.Count) {
    Write-Host "Equality Proof: PASS (Counts match)"
} else {
    throw "License inventory count ($licenseCount) does not equal resolved unique package count ($($uniquePackages.Count))"
}


Write-Host "`n--- 4. Lock-file verification (Pre-Restore) ---"
$preHashes = @{}

$allLocksRaw = Run-Command "git" @("ls-files", "--others", "--cached", "--exclude-standard")
$allLocks = $allLocksRaw.Stdout -split "`n" | Where-Object { $_.Trim() -match "packages\.lock\.json$" }
if ($allLocks.Count -ne 9) { throw "Expected exactly 9 lock files, found $($allLocks.Count)" }

foreach ($lock in $expectedLocks) {
    if (-not (Test-Path $lock)) { throw "Lock file missing: $lock" }

    $checkIgnore = Run-Command "git" @("check-ignore", $lock) -ExpectFailure
    $ignored = ($checkIgnore.ExitCode -eq 0)
    if ($ignored) { throw "Lock file is ignored by Git: $lock" }

    try {
        $null = Get-Content $lock -Raw | ConvertFrom-Json
    } catch {
        throw "Lock file is not valid JSON: $lock"
    }

    $hash = (Get-FileHash $lock -Algorithm SHA256).Hash
    $size = (Get-Item $lock).Length
    $preHashes[$lock] = @{ Hash = $hash; Size = $size; Ignored = $ignored }
}

Write-Host "`n--- 5. Repository and Cleanup Safety ---"
$itemsToDelete = Get-ChildItem -Path $gitRoot -Recurse -Force -Include "bin","obj","TestResults" | Where-Object { $_.PSIsContainer }
foreach ($item in $itemsToDelete) {
    if (-not $item.FullName.StartsWith($gitRoot + [System.IO.Path]::DirectorySeparatorChar)) {
        throw "Delete target escapes Git root: $($item.FullName)"
    }
    if ($item.Attributes -match "ReparsePoint") {
        throw "Delete target is a ReparsePoint: $($item.FullName)"
    }
    Remove-Item -Path $item.FullName -Recurse -Force
}

Write-Host "`n--- 6. Locked Restore ---"
$null = Run-Command "dotnet" @("restore", "DXOS.slnx", "--locked-mode")

Write-Host "`n--- 7. Lock-file verification (Post-Restore) ---"
$postLocks = Get-ChildItem -Path $gitRoot -Filter packages.lock.json -Recurse | Select-Object -ExpandProperty FullName
if ($postLocks.Count -ne $expectedLocks.Count) {
    throw "Lock file count changed after restore"
}
Write-Host "File | Before Size | Before Hash | After Size | After Hash | Ignored | Unchanged"
Write-Host "---- | ----------- | ----------- | ---------- | ---------- | ------- | ---------"
foreach ($lock in $expectedLocks) {
    $hash = (Get-FileHash $lock -Algorithm SHA256).Hash
    $size = (Get-Item $lock).Length
    $pre = $preHashes[$lock]

    $relPath = $lock.Replace($gitRoot, "").TrimStart("\").TrimStart("/")
    $unchanged = ($hash -eq $pre.Hash -and $size -eq $pre.Size)
    Write-Host "$relPath | $($pre.Size) | $($pre.Hash) | $size | $hash | $($pre.Ignored) | $unchanged"

    if (-not $unchanged) {
        throw "Lock file $lock mutated during locked restore"
    }
}

Write-Host "`n--- 8. Release Build ---"
$buildRaw = Run-Command "dotnet" @("build", "DXOS.slnx", "-c", "Release", "--no-restore", "-warnaserror")
if ($buildRaw.Stdout -match "(\d+) Warning\(s\)") {
    if ([int]$matches[1] -ne 0) { throw "Warnings detected in build" }
}
if ($buildRaw.Stdout -match "(\d+) Error\(s\)") {
    if ([int]$matches[1] -ne 0) { throw "Errors detected in build" }
}
foreach ($proj in $expectedProjects) {
    $name = [System.IO.Path]::GetFileNameWithoutExtension($proj)
    if (-not ($buildRaw.Stdout -match "$name ->")) {
        throw "Project $name did not appear in build output"
    }
}

Write-Host "`n--- 9. Hygiene and Tracked Artifacts ---"
$gitDiffCheck = Run-Command "git" @("diff", "--check") -ExpectFailure
if ($gitDiffCheck.ExitCode -ne 0) { throw "git diff --check failed (trailing whitespace or conflict markers in tracked changes)" }

$gitStatus = Run-Command "git" @("status", "--short")
$gitStatusLines = $gitStatus.Stdout -split "`n" | Where-Object { $_.Trim() }

$expectedStatus = @(
    " M .beads/interactions.jsonl",
    " M artifacts/task-runs/open_source-cab.2/implementation-report.md",
    " M artifacts/task-runs/open_source-cab.2/review.md",
    " M artifacts/task-runs/open_source-cab.2/verification-output.sha256",
    " M artifacts/task-runs/open_source-cab.2/verification-output.txt",
    " M artifacts/task-runs/open_source-cab.2/verification.md",
    " M artifacts/task-runs/open_source-cab.2/verify-r2.ps1",
    " M openspec/changes/bootstrap-remediation-001/tasks.md"
)

$expectedCounts = @{}
foreach ($s in $expectedStatus) {
    if ($expectedCounts.ContainsKey($s)) { throw "Duplicate expected Git status entry: $s" }
    $expectedCounts[$s] = 1
}

$actualStatusList = @($gitStatusLines | Sort-Object)
$expectedStatusList = @($expectedStatus | Sort-Object)

foreach ($s in $actualStatusList) {
    if (-not $expectedCounts.ContainsKey($s)) {
        throw "Unexpected actual Git status entry: $s"
    }
}
foreach ($s in $expectedStatusList) {
    if (-not ($actualStatusList -contains $s)) {
        throw "Missing expected Git status entry: $s"
    }
}

$e2eFiles = Get-ChildItem -Path $gitRoot -Recurse -Filter "*DXOS.E2E.Tests*" | Select-Object -ExpandProperty FullName
if ($e2eFiles.Count -gt 0) { throw "E2E project files still present in working tree" }

$scanSet = @()
foreach ($line in $gitStatusLines) {
    if ($line -match "^(.{2})\s+(.*)$") {
        $status = $matches[1]
        $path = $matches[2].Trim()
        if ($status -eq "??") {
            if ($path.EndsWith("/")) {
                $absDir = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($gitRoot, $path))
                if (Test-Path $absDir) {
                    Get-ChildItem -Path $absDir -File -Recurse | ForEach-Object { $scanSet += $_.FullName }
                }
            } else {
                $abs = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($gitRoot, $path))
                if (Test-Path $abs -PathType Leaf) { $scanSet += $abs }
            }
        } elseif ($status -match "[AMR]") {
            $abs = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($gitRoot, $path))
            if (Test-Path $abs -PathType Leaf) { $scanSet += $abs }
        }
    }
}
$rootConfigs = @("DXOS.slnx", "NuGet.Config", "global.json", "Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props", ".editorconfig", ".gitignore")
foreach ($conf in $rootConfigs) {
    $abs = [System.IO.Path]::GetFullPath([System.IO.Path]::Combine($gitRoot, $conf))
    if (Test-Path $abs -PathType Leaf) { $scanSet += $abs }
}

$scanSet = $scanSet | Select-Object -Unique | Where-Object { $_ -match "\.(cs|csproj|md|json|ps1|txt|editorconfig|props|targets|config|slnx|yaml|yml|gitignore)$" -and $_ -notmatch "verification-output\.txt$" -and $_ -notmatch "verification-output\.sha256$" }

$markerMC3 = [string][char]0x004D + [char]0x00C3
$markerC3A1 = [string][char]0x00C3 + [char]0x00A1
$markerE220AC = [string][char]0x00E2 + [char]0x20AC
$replacementMarker = [string][char]0xFFFD

foreach ($f in $scanSet) {
    if ($f -match "\\\.git\\" -or $f -match "\\bin\\" -or $f -match "\\obj\\" -or $f -match "\\\.beads\\" -or $f -match "verification-output\.txt" -or $f -match "verification-output\.sha256" -or $f -match "review\.md" -or $f -match "prompt\.md") {
        continue
    }
    try {
        $text = [System.IO.File]::ReadAllText($f, [System.Text.UTF8Encoding]::new($false, $true))
    } catch {
        throw "Failed to strictly decode as UTF-8: $f"
    }
    if ($text -match "[\x00-\x08\x0B\x0C\x0E-\x1F]") {
        throw "Control characters found in $f"
    }
    if ($text -match "$markerMC3|$markerC3A1|$markerE220AC|$replacementMarker") {
        throw "Mojibake found in $f"
    }
    if ($f -match "\.(md|ps1|json|cs|editorconfig|props|targets|config|slnx|yaml|yml|gitignore)$") {
        $lines = $text -split "`n"
        for ($i = 0; $i -lt $lines.Count; $i++) {
            if ($lines[$i] -match "[ \t]+`r?$") {
                throw "Trailing whitespace in $f on line $($i+1)"
            }
        }
    }
}

$null = Run-Command "openspec.cmd" @("validate", "bootstrap-remediation-001", "--type", "change", "--strict", "--no-interactive")
$null = Run-Command "bd.cmd" @("dep", "cycles")


$TotalTime = (Get-Date) - $StartTime
Write-Host "`n=========================================="
Write-Host "VERIFICATION SUCCESS" -ForegroundColor Green
Write-Host "Total Duration: $($TotalTime.TotalMilliseconds)ms"
Write-Host "=========================================="
