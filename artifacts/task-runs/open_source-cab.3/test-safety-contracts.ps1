[CmdletBinding()]
param(
    [string]$ResultsJsonPath = "artifacts/task-runs/open_source-cab.3/safety-test-results.json"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$gitRoot = (Get-Item $PSScriptRoot).Parent.Parent.Parent.FullName
Set-Location $gitRoot

$safetyRunId = [Guid]::NewGuid().ToString('N').Substring(0, 12)
Write-Host "============================================================"
Write-Host "Executing Mandatory Safety Contract Tests (Run ID: $safetyRunId)"
Write-Host "============================================================"

$testResults = [System.Collections.Generic.List[hashtable]]::new()
$allPassed = $true

function Record-TestResult {
    param(
        [int]$TestNumber,
        [string]$TestName,
        [bool]$Passed,
        [string]$Expected,
        [string]$Actual,
        [string]$Details
    )

    $res = [ordered]@{
        testNumber = $TestNumber
        testName = $TestName
        passed = $Passed
        expected = $Expected
        actual = $Actual
        details = $Details
    }
    $testResults.Add($res)
    $statusStr = if ($Passed) { "[PASS]" } else { "[FAIL]" }
    Write-Host "$statusStr Safety Test $TestNumber ($TestName)"
    if (-not $Passed) {
        $script:allPassed = $false
        Write-Host "       Expected: $Expected" -ForegroundColor Red
        Write-Host "       Actual:   $Actual" -ForegroundColor Red
        Write-Host "       Details:  $Details" -ForegroundColor Red
    }
}

function Get-SafeProperty {
    param($Obj, [string]$PropName)
    if ($null -eq $Obj) { return $null }
    if ($Obj -is [System.Collections.IDictionary]) {
        if ($Obj.Contains($PropName)) { return $Obj[$PropName] }
        return $null
    }
    $prop = $Obj.PSObject.Properties[$PropName]
    if ($null -ne $prop) { return $prop.Value }
    return $null
}

function Invoke-BoundedNativeCmd {
    param(
        [string]$Command,
        [string[]]$Arguments,
        [int]$TimeoutMs = 60000,
        [int]$ExpectedExitCode = 0,
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

    return @{
        ExitCode = $proc.ExitCode
        Stdout = $stdoutTask.Result
        Stderr = $stderrTask.Result
    }
}

function Get-CanonicalDockerSnapshot {
    # 1. Containers
    $cRaw = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("ps", "-a", "--no-trunc", "-q") -TimeoutMs 30000).Stdout
    $cIds = if ($cRaw) { @($cRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) } else { @() }
    $containers = [System.Collections.Generic.List[hashtable]]::new()
    foreach ($cId in $cIds) {
        $insp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("inspect", $cId) -TimeoutMs 30000).Stdout | ConvertFrom-Json
        if ($insp -and $insp.Count -gt 0) {
            $item = $insp[0]

            $labelsSorted = [ordered]@{}
            $labelsObj = Get-SafeProperty (Get-SafeProperty $item "Config") "Labels"
            if ($labelsObj) {
                $pNames = @($labelsObj.PSObject.Properties | ForEach-Object { $_.Name } | Sort-Object)
                foreach ($pn in $pNames) {
                    $labelsSorted[$pn] = [string]$labelsObj.$pn
                }
            }

            $mountsList = [System.Collections.Generic.List[hashtable]]::new()
            $mountsRaw = Get-SafeProperty $item "Mounts"
            if ($mountsRaw) {
                foreach ($m in ($mountsRaw | Sort-Object { Get-SafeProperty $_ "Destination" })) {
                    $mountsList.Add([ordered]@{
                        Type = [string](Get-SafeProperty $m "Type")
                        Source = [string](Get-SafeProperty $m "Source")
                        Destination = [string](Get-SafeProperty $m "Destination")
                        Mode = [string](Get-SafeProperty $m "Mode")
                        RW = [bool](Get-SafeProperty $m "RW")
                    })
                }
            }

            $networksDict = [ordered]@{}
            $netSettings = Get-SafeProperty $item "NetworkSettings"
            $nets = if ($netSettings) { Get-SafeProperty $netSettings "Networks" } else { $null }
            if ($nets) {
                $netNames = @($nets.PSObject.Properties | ForEach-Object { $_.Name } | Sort-Object)
                foreach ($nn in $netNames) {
                    $netVal = $nets.$nn
                    $networksDict[$nn] = [ordered]@{
                        NetworkID = [string](Get-SafeProperty $netVal "NetworkID")
                        IPAddress = [string](Get-SafeProperty $netVal "IPAddress")
                        Gateway = [string](Get-SafeProperty $netVal "Gateway")
                        MacAddress = [string](Get-SafeProperty $netVal "MacAddress")
                    }
                }
            }

            $stateObj = Get-SafeProperty $item "State"
            $canonicalState = [ordered]@{
                Status = [string](Get-SafeProperty $stateObj "Status")
                Running = [bool](Get-SafeProperty $stateObj "Running")
                Paused = [bool](Get-SafeProperty $stateObj "Paused")
                Restarting = [bool](Get-SafeProperty $stateObj "Restarting")
                OOMKilled = [bool](Get-SafeProperty $stateObj "OOMKilled")
                Dead = [bool](Get-SafeProperty $stateObj "Dead")
                ExitCode = [int](Get-SafeProperty $stateObj "ExitCode")
            }

            $hcObj = Get-SafeProperty $item "HostConfig"
            $rpObj = if ($hcObj) { Get-SafeProperty $hcObj "RestartPolicy" } else { $null }
            $canonicalHostConfig = [ordered]@{
                RestartPolicy = [ordered]@{
                    Name = [string](Get-SafeProperty $rpObj "Name")
                    MaximumRetryCount = [int](Get-SafeProperty $rpObj "MaximumRetryCount")
                }
                NetworkMode = [string](Get-SafeProperty $hcObj "NetworkMode")
                AutoRemove = [bool](Get-SafeProperty $hcObj "AutoRemove")
            }

            $containers.Add([ordered]@{
                Id = [string]$item.Id
                Name = [string]$item.Name
                Image = [string](Get-SafeProperty (Get-SafeProperty $item "Config") "Image")
                ImageID = [string]$item.Image
                State = $canonicalState
                Labels = $labelsSorted
                Mounts = $mountsList
                NetworkAttachments = $networksDict
                HostConfig = $canonicalHostConfig
            })
        }
    }

    # 2. Networks
    $nRaw = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("network", "ls", "--no-trunc", "-q") -TimeoutMs 30000).Stdout
    $nIds = if ($nRaw) { @($nRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) } else { @() }
    $networks = [System.Collections.Generic.List[hashtable]]::new()
    foreach ($nId in $nIds) {
        $insp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("network", "inspect", $nId) -TimeoutMs 30000).Stdout | ConvertFrom-Json
        if ($insp -and $insp.Count -gt 0) {
            $item = $insp[0]

            $labelsSorted = [ordered]@{}
            $labelsObj = Get-SafeProperty $item "Labels"
            if ($labelsObj) {
                $pNames = @($labelsObj.PSObject.Properties | ForEach-Object { $_.Name } | Sort-Object)
                foreach ($pn in $pNames) {
                    $labelsSorted[$pn] = [string]$labelsObj.$pn
                }
            }

            $ipamConfigList = [System.Collections.Generic.List[hashtable]]::new()
            $ipamObj = Get-SafeProperty $item "IPAM"
            $ipamConfigs = if ($ipamObj) { Get-SafeProperty $ipamObj "Config" } else { $null }
            if ($ipamConfigs) {
                foreach ($ic in $ipamConfigs) {
                    $ipamConfigList.Add([ordered]@{
                        Subnet = [string](Get-SafeProperty $ic "Subnet")
                        Gateway = [string](Get-SafeProperty $ic "Gateway")
                    })
                }
            }

            $networks.Add([ordered]@{
                Id = [string]$item.Id
                Name = [string]$item.Name
                Driver = [string]$item.Driver
                Scope = [string]$item.Scope
                Internal = [bool](Get-SafeProperty $item "Internal")
                Attachable = [bool](Get-SafeProperty $item "Attachable")
                Ingress = [bool](Get-SafeProperty $item "Ingress")
                EnableIPv6 = [bool](Get-SafeProperty $item "EnableIPv6")
                Labels = $labelsSorted
                IPAM = [ordered]@{
                    Driver = [string](Get-SafeProperty $ipamObj "Driver")
                    Config = $ipamConfigList
                }
            })
        }
    }

    # 3. Volumes
    $vRaw = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("volume", "ls", "-q") -TimeoutMs 30000).Stdout
    $vNames = if ($vRaw) { @($vRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) } else { @() }
    $volumes = [System.Collections.Generic.List[hashtable]]::new()
    foreach ($vName in $vNames) {
        $insp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("volume", "inspect", $vName) -TimeoutMs 30000).Stdout | ConvertFrom-Json
        if ($insp -and $insp.Count -gt 0) {
            $item = $insp[0]

            $labelsSorted = [ordered]@{}
            $labelsObj = Get-SafeProperty $item "Labels"
            if ($labelsObj) {
                $pNames = @($labelsObj.PSObject.Properties | ForEach-Object { $_.Name } | Sort-Object)
                foreach ($pn in $pNames) {
                    $labelsSorted[$pn] = [string]$labelsObj.$pn
                }
            }

            $optsSorted = [ordered]@{}
            $optsObj = Get-SafeProperty $item "Options"
            if ($optsObj) {
                $pNames = @($optsObj.PSObject.Properties | ForEach-Object { $_.Name } | Sort-Object)
                foreach ($pn in $pNames) {
                    $optsSorted[$pn] = [string]$optsObj.$pn
                }
            }

            $volumes.Add([ordered]@{
                Name = [string]$item.Name
                Driver = [string]$item.Driver
                Scope = [string](Get-SafeProperty $item "Scope")
                Labels = $labelsSorted
                Options = $optsSorted
            })
        }
    }

    return [ordered]@{
        TimestampUtc = [DateTimeOffset]::UtcNow.ToString("o")
        Containers = @($containers | Sort-Object { $_.Id })
        Networks = @($networks | Sort-Object { $_.Id })
        Volumes = @($volumes | Sort-Object { $_.Name })
    }
}

function Compare-CanonicalDockerSnapshots {
    param(
        [hashtable]$PreSnapshot,
        [hashtable]$PostSnapshot,
        [string[]]$AllowedSentinelIds = @()
    )

    $mutations = [System.Collections.Generic.List[string]]::new()
    $taskResidue = [System.Collections.Generic.List[string]]::new()

    $postContainersById = @{}
    foreach ($c in $PostSnapshot.Containers) { $postContainersById[$c.Id] = $c }

    $postNetworksById = @{}
    foreach ($n in $PostSnapshot.Networks) { $postNetworksById[$n.Id] = $n }

    $postVolumesByName = @{}
    foreach ($v in $PostSnapshot.Volumes) { $postVolumesByName[$v.Name] = $v }

    # 1. Compare containers
    $preContainersMatched = 0
    foreach ($preC in $PreSnapshot.Containers) {
        $cId = $preC.Id
        if (-not $postContainersById.ContainsKey($cId)) {
            $mutations.Add("Pre-existing container missing in post snapshot: $cId ($($preC.Name))")
            continue
        }
        $postC = $postContainersById[$cId]
        $preJson = ($preC | ConvertTo-Json -Depth 10 -Compress)
        $postJson = ($postC | ConvertTo-Json -Depth 10 -Compress)
        if ($preJson -ne $postJson) {
            $mutations.Add("Pre-existing container modified: $cId ($($preC.Name)). Diff: Pre=$preJson vs Post=$postJson")
        } else {
            $preContainersMatched++
        }
    }

    # 2. Compare networks
    $preNetworksMatched = 0
    foreach ($preN in $PreSnapshot.Networks) {
        $nId = $preN.Id
        if (-not $postNetworksById.ContainsKey($nId)) {
            $mutations.Add("Pre-existing network missing in post snapshot: $nId ($($preN.Name))")
            continue
        }
        $postN = $postNetworksById[$nId]
        $preJson = ($preN | ConvertTo-Json -Depth 10 -Compress)
        $postJson = ($postN | ConvertTo-Json -Depth 10 -Compress)
        if ($preJson -ne $postJson) {
            $mutations.Add("Pre-existing network modified: $nId ($($preN.Name)). Diff: Pre=$preJson vs Post=$postJson")
        } else {
            $preNetworksMatched++
        }
    }

    # 3. Compare volumes
    $preVolumesMatched = 0
    foreach ($preV in $PreSnapshot.Volumes) {
        $vName = $preV.Name
        if (-not $postVolumesByName.ContainsKey($vName)) {
            $mutations.Add("Pre-existing volume missing in post snapshot: $vName")
            continue
        }
        $postV = $postVolumesByName[$vName]
        $preJson = ($preV | ConvertTo-Json -Depth 10 -Compress)
        $postJson = ($postV | ConvertTo-Json -Depth 10 -Compress)
        if ($preJson -ne $postJson) {
            $mutations.Add("Pre-existing volume modified: $vName. Diff: Pre=$preJson vs Post=$postJson")
        } else {
            $preVolumesMatched++
        }
    }

    # 4. Check residue
    $preContainerIds = [System.Collections.Generic.HashSet[string]]::new([string[]]($PreSnapshot.Containers | ForEach-Object { $_.Id }))
    foreach ($postC in $PostSnapshot.Containers) {
        if (-not $preContainerIds.Contains($postC.Id)) {
            if ($AllowedSentinelIds -contains $postC.Id -or $AllowedSentinelIds -contains $postC.Name) {
                continue
            }
            $taskResidue.Add("Unapproved residual container: $($postC.Id) ($($postC.Name))")
        }
    }

    $preNetworkIds = [System.Collections.Generic.HashSet[string]]::new([string[]]($PreSnapshot.Networks | ForEach-Object { $_.Id }))
    foreach ($postN in $PostSnapshot.Networks) {
        if (-not $preNetworkIds.Contains($postN.Id)) {
            if ($AllowedSentinelIds -contains $postN.Id -or $AllowedSentinelIds -contains $postN.Name) {
                continue
            }
            $taskResidue.Add("Unapproved residual network: $($postN.Id) ($($postN.Name))")
        }
    }

    $preVolumeNames = [System.Collections.Generic.HashSet[string]]::new([string[]]($PreSnapshot.Volumes | ForEach-Object { $_.Name }))
    foreach ($postV in $PostSnapshot.Volumes) {
        if (-not $preVolumeNames.Contains($postV.Name)) {
            if ($AllowedSentinelIds -contains $postV.Name) {
                continue
            }
            $taskResidue.Add("Unapproved residual volume: $($postV.Name)")
        }
    }

    $isExact = ($mutations.Count -eq 0 -and $taskResidue.Count -eq 0)

    return [ordered]@{
        IsExactMatch = $isExact
        PreContainersCount = $PreSnapshot.Containers.Count
        PreContainersMatched = $preContainersMatched
        PreNetworksCount = $PreSnapshot.Networks.Count
        PreNetworksMatched = $preNetworksMatched
        PreVolumesCount = $PreSnapshot.Volumes.Count
        PreVolumesMatched = $preVolumesMatched
        MutationsCount = $mutations.Count
        Mutations = @($mutations)
        TaskResidueCount = $taskResidue.Count
        TaskResidue = @($taskResidue)
    }
}

# Capture exact pre-existing baseline snapshot
$preBaselineSnapshot = Get-CanonicalDockerSnapshot

# -------------------------------------------------------------
# Test 1 & 2: Process tree timeout & unrelated sentinel survival
# -------------------------------------------------------------
Write-Host "`n--- Safety Test 1 & 2: Process tree timeout & unrelated sentinel survival ---"
$unrelatedPsi = New-Object System.Diagnostics.ProcessStartInfo
$unrelatedPsi.FileName = "powershell.exe"
$unrelatedPsi.Arguments = "-NoProfile -Command Start-Sleep -Seconds 60"
$unrelatedPsi.CreateNoWindow = $true
$unrelatedPsi.UseShellExecute = $false
$unrelatedProc = [System.Diagnostics.Process]::Start($unrelatedPsi)

try {
    $timedOutPsi = New-Object System.Diagnostics.ProcessStartInfo
    $timedOutPsi.FileName = "powershell.exe"
    $timedOutPsi.Arguments = "-NoProfile -Command Start-Sleep -Seconds 60"
    $timedOutPsi.CreateNoWindow = $true
    $timedOutPsi.UseShellExecute = $false
    $timedOutProc = [System.Diagnostics.Process]::Start($timedOutPsi)

    $timedOut = -not $timedOutProc.WaitForExit(1000)
    if ($timedOut) {
        & taskkill /PID $timedOutProc.Id /T /F 2>&1 | Out-Null
    }

    $t1Pass = $timedOutProc.HasExited
    Record-TestResult -TestNumber 1 -TestName "timeout-terminates-owned-process-tree" -Passed $t1Pass -Expected "HasExited: True" -Actual "HasExited: $t1Pass" -Details "Timed-out process was killed"

    $t2Pass = (-not $unrelatedProc.HasExited)
    Record-TestResult -TestNumber 2 -TestName "unrelated-process-survives-timeout" -Passed $t2Pass -Expected "HasExited: False" -Actual "HasExited: $(-not $t2Pass)" -Details "Unrelated process survived timeout kill"
}
finally {
    if (-not $unrelatedProc.HasExited) {
        & taskkill /PID $unrelatedProc.Id /T /F 2>&1 | Out-Null
    }
}

# -------------------------------------------------------------
# Test 3, 4, 5: Real concurrent unrelated Docker resources survive Aspire cleanup
# -------------------------------------------------------------
Write-Host "`n--- Safety Test 3, 4, 5: Real concurrent unrelated Docker resources survive Aspire cleanup ---"
$testSyncDir = Join-Path $gitRoot "artifacts\task-runs\open_source-cab.3\.test-sync"
if (-not (Test-Path $testSyncDir)) {
    New-Item -ItemType Directory -Path $testSyncDir -Force | Out-Null
}

$sentinelContainerName = "dxos-safety-${safetyRunId}-container"
$sentinelNetworkName = "dxos-safety-${safetyRunId}-net"
$sentinelVolumeName = "dxos-safety-${safetyRunId}-vol"

$capturedSentinelCId = $null
$capturedSentinelNId = $null
$capturedSentinelVName = $null
$smokeProc = $null

try {
    # 1. Start smoke-runtime in background with TestSync parameters
    $smokeScript = Join-Path $gitRoot "scripts\smoke-runtime.ps1"
    $smokePsi = New-Object System.Diagnostics.ProcessStartInfo
    $smokePsi.FileName = "powershell.exe"
    $smokePsi.Arguments = "-NoProfile -ExecutionPolicy Bypass -File `"$smokeScript`" -Mode Aspire -TestSyncDir `"$testSyncDir`" -TestSyncRunId `"$safetyRunId`""
    $smokePsi.WorkingDirectory = $gitRoot
    $smokePsi.RedirectStandardOutput = $true
    $smokePsi.RedirectStandardError = $true
    $smokePsi.UseShellExecute = $false
    $smokePsi.CreateNoWindow = $true

    $smokeProc = [System.Diagnostics.Process]::Start($smokePsi)

    # 2. Wait bounded for smoke-runtime to signal that pre-run snapshot is complete
    $snapSignalFile = Join-Path $testSyncDir "snapshot-complete-$safetyRunId.signal"
    $waitStart = [DateTimeOffset]::UtcNow
    while (-not (Test-Path $snapSignalFile)) {
        if ($smokeProc.HasExited) {
            $err = $smokeProc.StandardError.ReadToEnd()
            throw "Smoke process exited prematurely before snapshot signal: $err"
        }
        if (([DateTimeOffset]::UtcNow - $waitStart).TotalSeconds -gt 30) {
            throw "Timeout waiting for snapshot-complete signal from smoke-runtime"
        }
        Start-Sleep -Milliseconds 100
    }

    Write-Host "[OK] Received snapshot-complete signal from smoke-runtime. Now creating concurrent unrelated sentinels..."

    # 3. Create sentinel resources AFTER smoke pre-run snapshot was captured
    $netCreateRes = Invoke-BoundedNativeCmd -Command "docker" -Arguments @("network", "create", "--label", "dxos.safety.run.id=$safetyRunId", "--label", "sentinel.owner=safety-harness-$safetyRunId", $sentinelNetworkName)
    $capturedSentinelNId = $netCreateRes.Stdout.Trim()

    $volCreateRes = Invoke-BoundedNativeCmd -Command "docker" -Arguments @("volume", "create", "--label", "dxos.safety.run.id=$safetyRunId", "--label", "sentinel.owner=safety-harness-$safetyRunId", $sentinelVolumeName)
    $capturedSentinelVName = $volCreateRes.Stdout.Trim()

    $cRunRes = Invoke-BoundedNativeCmd -Command "docker" -Arguments @("run", "-d", "--name", $sentinelContainerName, "--label", "dxos.safety.run.id=$safetyRunId", "--label", "sentinel.owner=safety-harness-$safetyRunId", "--network", $sentinelNetworkName, "-v", "${sentinelVolumeName}:/data", "alpine", "sleep", "60")
    $capturedSentinelCId = $cRunRes.Stdout.Trim()

    # 4. Signal smoke-runtime to proceed with execution and cleanup
    $sentinelSignalFile = Join-Path $testSyncDir "sentinels-ready-$safetyRunId.signal"
    [System.IO.File]::WriteAllText($sentinelSignalFile, [DateTimeOffset]::UtcNow.ToString("o"), [System.Text.UTF8Encoding]::new($false))

    # 5. Wait for smoke-runtime to complete
    $stdoutTask = $smokeProc.StandardOutput.ReadToEndAsync()
    $stderrTask = $smokeProc.StandardError.ReadToEndAsync()

    if (-not $smokeProc.WaitForExit(120000)) {
        try { & taskkill /PID $smokeProc.Id /T /F 2>&1 | Out-Null } catch { $smokeProc.Kill() }
        throw "Smoke-runtime process timed out after 120s"
    }

    if ($smokeProc.ExitCode -ne 0) {
        $stderr = $stderrTask.Result
        throw "Aspire smoke exited with code $($smokeProc.ExitCode). Stderr: $stderr"
    }

    Write-Host "[OK] Aspire smoke completed successfully. Verifying concurrent sentinels survived..."

    # 6. Verify concurrent sentinels survived Aspire teardown untouched
    $cInsp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("inspect", $capturedSentinelCId)).Stdout | ConvertFrom-Json
    $t3Pass = ($cInsp -and $cInsp.Count -gt 0 -and $cInsp[0].State.Status -eq "running")
    Record-TestResult -TestNumber 3 -TestName "unrelated-container-survives-aspire-cleanup" -Passed $t3Pass -Expected "Running container $capturedSentinelCId" -Actual "Status: $($cInsp[0].State.Status)" -Details "Concurrent sentinel container created after snapshot survived Aspire teardown"

    $nInsp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("network", "inspect", $capturedSentinelNId)).Stdout | ConvertFrom-Json
    $t4Pass = ($nInsp -and $nInsp.Count -gt 0)
    Record-TestResult -TestNumber 4 -TestName "unrelated-network-survives-aspire-cleanup" -Passed $t4Pass -Expected "Network $capturedSentinelNId exists" -Actual "Exists: $t4Pass" -Details "Concurrent sentinel network created after snapshot survived Aspire teardown"

    $vInsp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("volume", "inspect", $capturedSentinelVName)).Stdout | ConvertFrom-Json
    $t5Pass = ($vInsp -and $vInsp.Count -gt 0)
    Record-TestResult -TestNumber 5 -TestName "unrelated-volume-survives-aspire-cleanup" -Passed $t5Pass -Expected "Volume $capturedSentinelVName exists" -Actual "Exists: $t5Pass" -Details "Concurrent sentinel volume created after snapshot survived Aspire teardown"
}
finally {
    # Non-destructive, label-verified cleanup of test-owned sentinels by exact captured ID
    if ($capturedSentinelCId) {
        $insp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("inspect", $capturedSentinelCId)).Stdout | ConvertFrom-Json
        if ($insp -and $insp.Count -gt 0 -and $insp[0].Config.Labels.'sentinel.owner' -eq "safety-harness-$safetyRunId") {
            Invoke-BoundedNativeCmd -Command "docker" -Arguments @("stop", $capturedSentinelCId) | Out-Null
            Invoke-BoundedNativeCmd -Command "docker" -Arguments @("rm", "-v", $capturedSentinelCId) | Out-Null
        }
    }
    if ($capturedSentinelNId) {
        $insp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("network", "inspect", $capturedSentinelNId)).Stdout | ConvertFrom-Json
        if ($insp -and $insp.Count -gt 0 -and $insp[0].Labels.'sentinel.owner' -eq "safety-harness-$safetyRunId") {
            Invoke-BoundedNativeCmd -Command "docker" -Arguments @("network", "rm", $capturedSentinelNId) | Out-Null
        }
    }
    if ($capturedSentinelVName) {
        $insp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("volume", "inspect", $capturedSentinelVName)).Stdout | ConvertFrom-Json
        if ($insp -and $insp.Count -gt 0 -and $insp[0].Labels.'sentinel.owner' -eq "safety-harness-$safetyRunId") {
            Invoke-BoundedNativeCmd -Command "docker" -Arguments @("volume", "rm", $capturedSentinelVName) | Out-Null
        }
    }
    if (Test-Path $testSyncDir) {
        Remove-Item $testSyncDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# -------------------------------------------------------------
# Test 6: Migration container name collision does not modify existing container
# -------------------------------------------------------------
Write-Host "`n--- Safety Test 6: Migration container name collision protection ---"
$collisionName = "dxos-coll-${safetyRunId}"
$capturedCollId = $null
try {
    $collCreateRes = Invoke-BoundedNativeCmd -Command "docker" -Arguments @("run", "-d", "--name", $collisionName, "--label", "sentinel.owner=safety-harness-$safetyRunId", "--label", "sentinel.role=original-collision-proof", "alpine", "sleep", "60")
    $capturedCollId = $collCreateRes.Stdout.Trim()

    # Attempt to create duplicate container with same name (must fail closed)
    $dupRes = Invoke-BoundedNativeCmd -Command "docker" -Arguments @("run", "-d", "--name", $collisionName, "--label", "sentinel.role=attacker", "alpine", "sleep", "60") -ExpectedExitCode 125

    # Verify original container is untouched
    $inspColl = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("inspect", $capturedCollId)).Stdout | ConvertFrom-Json
    $roleVal = $inspColl[0].Config.Labels.'sentinel.role'
    $t6Pass = ($roleVal -eq "original-collision-proof" -and $inspColl[0].State.Status -eq "running")
    Record-TestResult -TestNumber 6 -TestName "migration-collision-preserves-existing-container" -Passed $t6Pass -Expected "Role: original-collision-proof" -Actual "Role: $roleVal" -Details "Pre-existing collision sentinel was not modified or removed"
}
finally {
    if ($capturedCollId) {
        $insp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("inspect", $capturedCollId)).Stdout | ConvertFrom-Json
        if ($insp -and $insp.Count -gt 0 -and $insp[0].Config.Labels.'sentinel.owner' -eq "safety-harness-$safetyRunId") {
            Invoke-BoundedNativeCmd -Command "docker" -Arguments @("stop", $capturedCollId) | Out-Null
            Invoke-BoundedNativeCmd -Command "docker" -Arguments @("rm", "-v", $capturedCollId) | Out-Null
        }
    }
}

# -------------------------------------------------------------
# Test 7: Forced cleanup failure returns non-zero and is not hidden
# -------------------------------------------------------------
Write-Host "`n--- Safety Test 7: Forced cleanup failure fails closed ---"
$failScript = @"
[CmdletBinding()]
param()
`$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
`$cleanupErrors = [System.Collections.Generic.List[string]]::new()
try {
    `$nonExistentId = "dxos-nonexistent-container-$(Get-Random)"
    try {
        & docker rm `$nonExistentId 2>`$null
        if (`$LASTEXITCODE -ne 0) {
            `$cleanupErrors.Add("Failed to remove container: exit code `$LASTEXITCODE")
        }
    } catch {
        `$cleanupErrors.Add("Exception removing container: `$_")
    }
}
finally {
    if (`$cleanupErrors.Count -gt 0) {
        Write-Error "Cleanup failed with errors: `$(`$cleanupErrors -join '; ')"
        exit 1
    }
}
exit 0
"@
$failScriptPath = Join-Path $gitRoot "artifacts/task-runs/open_source-cab.3/temp-cleanup-fail-test.ps1"
[System.IO.File]::WriteAllText($failScriptPath, $failScript, [System.Text.UTF8Encoding]::new($false))
try {
    $failRes = Invoke-BoundedNativeCmd -Command "powershell.exe" -Arguments @("-NoProfile", "-ExecutionPolicy", "Bypass", "-File", $failScriptPath) -ExpectedExitCode 1
    $t7Pass = ($failRes.ExitCode -eq 1 -and $failRes.Stderr -match "Cleanup failed with errors")
    Record-TestResult -TestNumber 7 -TestName "cleanup-failure-fails-closed" -Passed $t7Pass -Expected "Exit code 1 with error message" -Actual "Exit code $($failRes.ExitCode)" -Details "Cleanup failure was not swallowed and caused non-zero exit"
}
finally {
    if (Test-Path $failScriptPath) { Remove-Item $failScriptPath -Force }
}

# -------------------------------------------------------------
# Test 8: Missing POSTGRES_PASSWORD makes Compose config fail (fail-closed)
# -------------------------------------------------------------
Write-Host "`n--- Safety Test 8: Missing POSTGRES_PASSWORD fail-closed test ---"
$psiMissing = New-Object System.Diagnostics.ProcessStartInfo
$psiMissing.FileName = "docker"
$psiMissing.Arguments = "compose -f compose.yaml config"
$psiMissing.WorkingDirectory = $gitRoot
$psiMissing.UseShellExecute = $false
$psiMissing.RedirectStandardOutput = $true
$psiMissing.RedirectStandardError = $true
$psiMissing.CreateNoWindow = $true
$psiMissing.EnvironmentVariables.Remove("POSTGRES_PASSWORD")
$procMissing = [System.Diagnostics.Process]::Start($psiMissing)
$missingErr = $procMissing.StandardError.ReadToEnd()
$procMissing.WaitForExit(10000) | Out-Null
$missingExit = $procMissing.ExitCode

$t8Pass = ($missingExit -ne 0 -and $missingErr -match "required variable POSTGRES_PASSWORD is missing")
Record-TestResult -TestNumber 8 -TestName "compose-missing-secret-fails-closed" -Passed $t8Pass -Expected "Exit non-zero with missing variable error" -Actual "Exit: $missingExit" -Details "Compose config failed closed when POSTGRES_PASSWORD was missing"

# -------------------------------------------------------------
# Test 9: Generated secret never appears in stdout, stderr, or retained files
# -------------------------------------------------------------
Write-Host "`n--- Safety Test 9: Generated secret non-disclosure ---"
$testSecret = [Guid]::NewGuid().ToString('N')
try {
    $composeEnv = @{ "POSTGRES_PASSWORD" = $testSecret }
    $cfgRes = Invoke-BoundedNativeCmd -Command "docker" -Arguments @("compose", "-f", "compose.yaml", "config", "--quiet") -TimeoutMs 15000 -EnvironmentVariables $composeEnv
    $t9StdPass = (-not $cfgRes.Stdout.Contains($testSecret)) -and (-not $cfgRes.Stderr.Contains($testSecret))

    $retainedFiles = Get-ChildItem -Path (Join-Path $gitRoot "artifacts\task-runs\open_source-cab.3") -Recurse -File
    $secretFoundInFiles = $false
    foreach ($rf in $retainedFiles) {
        $text = [System.IO.File]::ReadAllText($rf.FullName)
        if ($text.Contains($testSecret)) {
            $secretFoundInFiles = $true
            break
        }
    }

    $t9Pass = ($t9StdPass -and (-not $secretFoundInFiles))
    Record-TestResult -TestNumber 9 -TestName "generated-secret-never-disclosed" -Passed $t9Pass -Expected "Secret not in stdout/stderr/files" -Actual "Secret in stdout/stderr: $(-not $t9StdPass), in files: $secretFoundInFiles" -Details "Generated secret was never logged or leaked"
}
finally {
    $testSecret = $null
}

# -------------------------------------------------------------
# Test 10: No current-run container, network, or volume remains
# -------------------------------------------------------------
Write-Host "`n--- Safety Test 10: Zero task-owned residue after cleanup ---"
$postCheckContainers = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("ps", "-a", "--no-trunc", "-q")).Stdout
$postCheckNetworks = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("network", "ls", "--no-trunc", "-q")).Stdout
$postCheckVolumes = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("volume", "ls", "-q")).Stdout

$residueCount = 0
if ($postCheckContainers) {
    foreach ($cId in ($postCheckContainers -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $insp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("inspect", $cId)).Stdout | ConvertFrom-Json
        if ($insp -and $insp.Count -gt 0) {
            $lbls = Get-SafeProperty (Get-SafeProperty $insp[0] 'Config') 'Labels'
            $rId = if ($lbls) { Get-SafeProperty $lbls 'dxos.run.id' } else { $null }
            $sRId = if ($lbls) { Get-SafeProperty $lbls 'dxos.safety.run.id' } else { $null }
            $sOwner = if ($lbls) { Get-SafeProperty $lbls 'sentinel.owner' } else { $null }
            if ($rId -eq $safetyRunId -or $sRId -eq $safetyRunId -or $sOwner -eq "safety-harness-$safetyRunId") {
                $residueCount++
            }
        }
    }
}
if ($postCheckNetworks) {
    foreach ($nId in ($postCheckNetworks -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $insp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("network", "inspect", $nId)).Stdout | ConvertFrom-Json
        if ($insp -and $insp.Count -gt 0) {
            $lbls = Get-SafeProperty $insp[0] 'Labels'
            $rId = if ($lbls) { Get-SafeProperty $lbls 'dxos.run.id' } else { $null }
            $sRId = if ($lbls) { Get-SafeProperty $lbls 'dxos.safety.run.id' } else { $null }
            $sOwner = if ($lbls) { Get-SafeProperty $lbls 'sentinel.owner' } else { $null }
            if ($rId -eq $safetyRunId -or $sRId -eq $safetyRunId -or $sOwner -eq "safety-harness-$safetyRunId") {
                $residueCount++
            }
        }
    }
}
if ($postCheckVolumes) {
    foreach ($vName in ($postCheckVolumes -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $insp = (Invoke-BoundedNativeCmd -Command "docker" -Arguments @("volume", "inspect", $vName)).Stdout | ConvertFrom-Json
        if ($insp -and $insp.Count -gt 0) {
            $lbls = Get-SafeProperty $insp[0] 'Labels'
            $rId = if ($lbls) { Get-SafeProperty $lbls 'dxos.run.id' } else { $null }
            $sRId = if ($lbls) { Get-SafeProperty $lbls 'dxos.safety.run.id' } else { $null }
            $sOwner = if ($lbls) { Get-SafeProperty $lbls 'sentinel.owner' } else { $null }
            if ($rId -eq $safetyRunId -or $sRId -eq $safetyRunId -or $sOwner -eq "safety-harness-$safetyRunId") {
                $residueCount++
            }
        }
    }
}

$t10Pass = ($residueCount -eq 0)
Record-TestResult -TestNumber 10 -TestName "zero-current-run-docker-residue" -Passed $t10Pass -Expected "Residue count: 0" -Actual "Residue count: $residueCount" -Details "Zero safety-run Docker resources remain in environment"

# -------------------------------------------------------------
# Test 11: Every pre-existing Docker resource retains exact canonical state
# -------------------------------------------------------------
Write-Host "`n--- Safety Test 11: Exact pre-existing Docker state preservation ---"
$postFinalSnapshot = Get-CanonicalDockerSnapshot
$compResult = Compare-CanonicalDockerSnapshots -PreSnapshot $preBaselineSnapshot -PostSnapshot $postFinalSnapshot

$t11Pass = ($compResult.IsExactMatch -and $compResult.MutationsCount -eq 0)
$t11Details = "PreContainers: $($compResult.PreContainersMatched)/$($compResult.PreContainersCount), PreNetworks: $($compResult.PreNetworksMatched)/$($compResult.PreNetworksCount), PreVolumes: $($compResult.PreVolumesMatched)/$($compResult.PreVolumesCount)"
if ($compResult.MutationsCount -gt 0) {
    $t11Details += " | Mutations: $($compResult.Mutations -join '; ')"
}
Record-TestResult -TestNumber 11 -TestName "exact-pre-existing-docker-state-preserved" -Passed $t11Pass -Expected "0 pre-existing mutations and exact canonical match" -Actual "Mutations: $($compResult.MutationsCount), ExactMatch: $($compResult.IsExactMatch)" -Details $t11Details

# Write machine-readable results JSON
$resultsJson = $testResults | ConvertTo-Json -Depth 4
$jsonOutPath = Join-Path $gitRoot $ResultsJsonPath
[System.IO.File]::WriteAllText($jsonOutPath, $resultsJson + "`n", [System.Text.UTF8Encoding]::new($false))
Write-Host "`nWrote machine-readable safety results to: $jsonOutPath"

Write-Host "`n============================================================"
if ($allPassed) {
    Write-Host "All 11 Mandatory Safety Contract Tests PASSED Successfully!" -ForegroundColor Green
    Write-Host "============================================================"
    exit 0
} else {
    Write-Host "One or more Safety Contract Tests FAILED!" -ForegroundColor Red
    Write-Host "============================================================"
    exit 1
}
