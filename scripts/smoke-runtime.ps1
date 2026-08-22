[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Compose', 'Aspire')]
    [string]$Mode,

    [int]$TimeoutSeconds = 120,
    [string]$EvidenceDir = "artifacts/quality-gate",
    [switch]$IncludeNegativeTest,
    [string]$TestSyncDir = $null,
    [string]$TestSyncRunId = $null
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

[Console]::OutputEncoding = [System.Text.UTF8Encoding]::new($false)
[Console]::InputEncoding = [System.Text.UTF8Encoding]::new($false)
$OutputEncoding = [System.Text.UTF8Encoding]::new($false)

$gitRoot = (Get-Item $PSScriptRoot).Parent.FullName
$currentDir = (Get-Item .).FullName
if ($currentDir -ne $gitRoot) {
    Write-Error "smoke-runtime.ps1 must be run from the canonical repository root: $gitRoot (current: $currentDir)"
    exit 1
}

$runId = [Guid]::NewGuid().ToString('N').Substring(0, 12)
$projectName = "dxos-smoke-$runId"
$apiPort = 18080 + (Get-Random -Minimum 10 -Maximum 900)
$postgresPort = 15432 + (Get-Random -Minimum 10 -Maximum 900)
$disposablePassword = [Guid]::NewGuid().ToString('N')

Write-Host "============================================================"
Write-Host "DX-OS Runtime Smoke Runner"
Write-Host "Mode: $Mode | Run ID: $runId | Project: $projectName"
Write-Host "Ports: API=$apiPort, Postgres=$postgresPort"
Write-Host "============================================================"

function Get-ObjectProperty {
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

$script:IsWindowsOs = ($env:OS -eq 'Windows_NT')

function Get-ChildProcessIds {
    param([int]$ParentId)

    $ids = New-Object System.Collections.Generic.List[int]
    if ($script:IsWindowsOs) {
        try {
            $children = Get-CimInstance Win32_Process -Filter "ParentProcessId = $ParentId" -ErrorAction SilentlyContinue
            if ($children) {
                foreach ($c in $children) {
                    $ids.Add([int]$c.ProcessId)
                }
            }
        } catch { }
        return $ids
    }

    try {
        $procDirs = Get-ChildItem -Path '/proc' -Directory -ErrorAction SilentlyContinue | Where-Object { $_.Name -match '^[0-9]+$' }
        foreach ($dir in $procDirs) {
            $statusPath = Join-Path $dir.FullName 'status'
            if (-not (Test-Path -LiteralPath $statusPath)) { continue }
            $parentPid = $null
            foreach ($line in [System.IO.File]::ReadLines($statusPath)) {
                if ($line.StartsWith('PPid:')) {
                    $parentPid = [int]($line.Substring(5).Trim())
                    break
                }
            }
            if ($parentPid -eq $ParentId) {
                $ids.Add([int]$dir.Name)
            }
        }
    } catch { }

    return $ids
}

function Stop-ProcessTreeById {
    param([int]$ProcessId)

    if ($ProcessId -le 0) { return }
    if ($script:IsWindowsOs) {
        try {
            & taskkill /PID $ProcessId /T /F 2>&1 | Out-Null
        } catch { }
        return
    }

    try { & kill -TERM $ProcessId 2>&1 | Out-Null } catch { }
    Start-Sleep -Milliseconds 200
    try { & kill -KILL $ProcessId 2>&1 | Out-Null } catch { }
}

function Write-AspireProcessLogs {
    param([string]$Reason)

    Write-Host "==> AppHost logs ($Reason)"
    try {
        if ($null -ne $aspireStdoutTask -and $aspireStdoutTask.Wait(4000)) {
            $stdoutText = $aspireStdoutTask.Result
            if (-not [string]::IsNullOrWhiteSpace($stdoutText)) {
                Write-Host "----- AppHost stdout -----"
                Write-Host $stdoutText
            } else {
                Write-Host "AppHost stdout was empty."
            }
        } else {
            Write-Host "AppHost stdout was still streaming (process may still be running)."
        }
    } catch {
        Write-Host "Failed to read AppHost stdout: $_"
    }

    try {
        if ($null -ne $aspireStderrTask -and $aspireStderrTask.Wait(4000)) {
            $stderrText = $aspireStderrTask.Result
            if (-not [string]::IsNullOrWhiteSpace($stderrText)) {
                Write-Host "----- AppHost stderr -----"
                Write-Host $stderrText
            }
        }
    } catch {
        Write-Host "Failed to read AppHost stderr: $_"
    }
}

function Invoke-BoundedNativeCommand {
    param(
        [string]$Command,
        [string[]]$Arguments,
        [int]$TimeoutMs = 120000,
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
            Stop-ProcessTreeById -ProcessId $proc.Id
        } catch {
            try { $proc.Kill() } catch { }
        }
        throw "Command '$Command $($psi.Arguments)' timed out after $($TimeoutMs / 1000)s"
    }

    $stdout = $stdoutTask.Result
    $stderr = $stderrTask.Result
    $exitCode = $proc.ExitCode

    if ($exitCode -ne $ExpectedExitCode) {
        throw "Command '$Command $($psi.Arguments)' exited with code $exitCode (expected $ExpectedExitCode). Stderr: $stderr"
    }

    return @{
        ExitCode = $exitCode
        Stdout = $stdout
        Stderr = $stderr
    }
}

function Refresh-OwnedProcessTree {
    foreach ($proc in $ownedProcesses) {
        try {
            if (-not $proc.HasExited) {
                $allOwnedPids.Add($proc.Id) | Out-Null
            }
        } catch { }
    }

    $queue = [System.Collections.Generic.Queue[int]]::new()
    foreach ($p in $allOwnedPids) {
        $queue.Enqueue($p)
    }

    while ($queue.Count -gt 0) {
        $curr = $queue.Dequeue()
        try {
            foreach ($childPid in (Get-ChildProcessIds -ParentId $curr)) {
                if ($allOwnedPids.Add($childPid)) {
                    $queue.Enqueue($childPid)
                }
            }
        } catch { }
    }
}

function Invoke-HttpRequestWithRetry {
    param(
        [string]$Url,
        [string]$Method = 'GET',
        [string]$Body = $null,
        [int]$MaxAttempts = 30,
        [int]$DelaySeconds = 2,
        [int[]]$ExpectedStatusCodes = @(200)
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        Refresh-OwnedProcessTree
        if ($null -ne $aspireProc) {
            $hostExited = $false
            $hostExitCode = $null
            try {
                if ($aspireProc.HasExited) {
                    $hostExited = $true
                    $hostExitCode = $aspireProc.ExitCode
                }
            } catch { }
            if ($hostExited) {
                try { Write-AspireProcessLogs -Reason "AppHost exited during wait for $Url" } catch { }
                throw "AppHost exited with code $hostExitCode while waiting for $Url"
            }
        }
        try {
            $params = @{
                Uri = $Url
                Method = $Method
                UseBasicParsing = $true
                TimeoutSec = 5
                ErrorAction = 'Stop'
            }
            if ($Body) {
                $params.Body = $Body
                $params.ContentType = 'application/json'
            }

            $response = Invoke-WebRequest @params
            $statusCode = [int]$response.StatusCode
            if ($ExpectedStatusCodes -contains $statusCode) {
                return @{
                    Success = $true
                    StatusCode = $statusCode
                    Content = $response.Content
                }
            }
        }
        catch [System.Net.WebException] {
            $ex = $_.Exception
            if ($ex.Response) {
                $resp = [System.Net.HttpWebResponse]$ex.Response
                $code = [int]$resp.StatusCode
                if ($ExpectedStatusCodes -contains $code) {
                    $reader = New-Object System.IO.StreamReader($resp.GetResponseStream(), [System.Text.UTF8Encoding]::new($false))
                    $content = $reader.ReadToEnd()
                    return @{
                        Success = $true
                        StatusCode = $code
                        Content = $content
                    }
                }
            }
        }
        catch {
            # Transient connection failure during startup, retry
        }

        Start-Sleep -Seconds $DelaySeconds
    }

    return @{
        Success = $false
        StatusCode = 0
        Content = $null
    }
}

$ownedProcesses = [System.Collections.Generic.List[System.Diagnostics.Process]]::new()
$allOwnedPids = [System.Collections.Generic.HashSet[int]]::new()
$ownedVolumeNames = [System.Collections.Generic.HashSet[string]]::new()
$cleanupErrors = [System.Collections.Generic.List[string]]::new()
$overallSuccess = $false
$smokeFailure = $null
$aspireStartTime = [DateTimeOffset]::UtcNow
$aspireProc = $null
$aspireStdoutTask = $null
$aspireStderrTask = $null

# Exact pre-run snapshots via bounded native calls
$preContainersRaw = (Invoke-BoundedNativeCommand -Command "docker" -Arguments @("ps", "-a", "--no-trunc", "-q") -TimeoutMs 30000).Stdout
$preExistingContainers = if ($preContainersRaw) {
    @($preContainersRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
} else { @() }

$preNetworksRaw = (Invoke-BoundedNativeCommand -Command "docker" -Arguments @("network", "ls", "--no-trunc", "-q") -TimeoutMs 30000).Stdout
$preExistingNetworks = if ($preNetworksRaw) {
    @($preNetworksRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
} else { @() }

$preVolumesRaw = (Invoke-BoundedNativeCommand -Command "docker" -Arguments @("volume", "ls", "-q") -TimeoutMs 30000).Stdout
$preExistingVolumes = if ($preVolumesRaw) {
    @($preVolumesRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
} else { @() }

# Test-only synchronization hook: signal snapshot completion and wait for sentinel creation
if ($TestSyncDir -and $TestSyncRunId) {
    if (-not (Test-Path $TestSyncDir)) {
        New-Item -ItemType Directory -Path $TestSyncDir -Force | Out-Null
    }
    $snapSignalFile = Join-Path $TestSyncDir "snapshot-complete-$TestSyncRunId.signal"
    [System.IO.File]::WriteAllText($snapSignalFile, [DateTimeOffset]::UtcNow.ToString("o"), [System.Text.UTF8Encoding]::new($false))

    $sentinelSignalFile = Join-Path $TestSyncDir "sentinels-ready-$TestSyncRunId.signal"
    $waitStart = [DateTimeOffset]::UtcNow
    while (-not (Test-Path $sentinelSignalFile)) {
        if (([DateTimeOffset]::UtcNow - $waitStart).TotalSeconds -gt 25) {
            throw "Test synchronization timeout: Sentinels ready signal not received within 25s at $sentinelSignalFile"
        }
        Start-Sleep -Milliseconds 100
    }
}

try {
    if ($Mode -eq 'Compose') {
        Write-Host "==> Starting Docker Compose environment ($projectName)..."
        $composeEnv = @{
            "COMPOSE_PROJECT_NAME" = $projectName
            "POSTGRES_PORT" = "$postgresPort"
            "API_PORT" = "$apiPort"
            "POSTGRES_DB" = "dxos"
            "POSTGRES_USER" = "dxos"
            "POSTGRES_PASSWORD" = $disposablePassword
        }

        Invoke-BoundedNativeCommand -Command "docker" -Arguments @("compose", "-p", $projectName, "-f", "compose.yaml", "up", "-d", "--build") -TimeoutMs ($TimeoutSeconds * 1000) -EnvironmentVariables $composeEnv
    }
    elseif ($Mode -eq 'Aspire') {
        Write-Host "==> Starting Aspire AppHost ($projectName)..."

        $dashboardPort = 19000 + (Get-Random -Minimum 10 -Maximum 900)
        $otlpPort = 19100 + (Get-Random -Minimum 10 -Maximum 900)
        $resourcePort = 19200 + (Get-Random -Minimum 10 -Maximum 900)
        if ($dashboardPort -eq $apiPort) { $dashboardPort++ }
        if ($otlpPort -eq $apiPort -or $otlpPort -eq $dashboardPort) { $otlpPort++ }
        if ($resourcePort -eq $apiPort -or $resourcePort -eq $dashboardPort -or $resourcePort -eq $otlpPort) { $resourcePort++ }

        Write-Host "Aspire bind: API=127.0.0.1:$apiPort dashboard=127.0.0.1:$dashboardPort otlp=127.0.0.1:$otlpPort resource=127.0.0.1:$resourcePort"

        $psi = New-Object System.Diagnostics.ProcessStartInfo
        $psi.FileName = "dotnet"
        $psi.Arguments = "run --project src/DXOS.AppHost/DXOS.AppHost.csproj --no-build -c Release --no-launch-profile"
        $psi.WorkingDirectory = $gitRoot
        $psi.UseShellExecute = $false
        $psi.RedirectStandardOutput = $true
        $psi.RedirectStandardError = $true
        $psi.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
        $psi.StandardErrorEncoding = [System.Text.UTF8Encoding]::new($false)
        $psi.CreateNoWindow = $true

        $psi.EnvironmentVariables["ASPNETCORE_URLS"] = "http://127.0.0.1:$dashboardPort"
        $psi.EnvironmentVariables["DOTNET_DASHBOARD_OTLP_ENDPOINT_URL"] = "http://127.0.0.1:$otlpPort"
        $psi.EnvironmentVariables["DOTNET_RESOURCE_SERVICE_ENDPOINT_URL"] = "http://127.0.0.1:$resourcePort"
        $psi.EnvironmentVariables["API_PORT"] = "$apiPort"
        $psi.EnvironmentVariables["POSTGRES_PORT"] = "$postgresPort"
        $psi.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Development"
        $psi.EnvironmentVariables["DOTNET_ENVIRONMENT"] = "Development"
        $psi.EnvironmentVariables["ASPIRE_ALLOW_UNSECURED_TRANSPORT"] = "true"
        $psi.EnvironmentVariables["DOTNET_ASPIRE_CONTAINER_RUNTIME"] = "docker"
        $psi.EnvironmentVariables["EngineeringSmoke__Enabled"] = "true"
        $psi.EnvironmentVariables["DXOS_RUN_ID"] = $runId

        $dcpRid = if ($script:IsWindowsOs) { "win-x64" } else { "linux-x64" }
        $dcpFile = if ($script:IsWindowsOs) { "dcp.exe" } else { "dcp" }
        $packagesRoot = if ($env:NUGET_PACKAGES) {
            $env:NUGET_PACKAGES
        } elseif ($env:HOME) {
            Join-Path $env:HOME ".nuget" "packages"
        } else {
            Join-Path $env:USERPROFILE ".nuget" "packages"
        }
        $dcpCandidate = Join-Path $packagesRoot "aspire.hosting.orchestration.$dcpRid" "13.4.6" "tools" $dcpFile
        if (Test-Path -LiteralPath $dcpCandidate) {
            $psi.EnvironmentVariables["DcpPublisher__CliPath"] = $dcpCandidate
            Write-Host "DCP cli path: $dcpCandidate"
        } else {
            Write-Host "DCP cli path not found at $dcpCandidate (AppHost will resolve host RID)."
        }

        $aspireProc = [System.Diagnostics.Process]::Start($psi)
        $ownedProcesses.Add($aspireProc)
        $allOwnedPids.Add($aspireProc.Id) | Out-Null
        $aspireStdoutTask = $aspireProc.StandardOutput.ReadToEndAsync()
        $aspireStderrTask = $aspireProc.StandardError.ReadToEndAsync()
        Write-Host "AppHost PID=$($aspireProc.Id) wait URL=http://127.0.0.1:$apiPort/health/live"
    }

    $baseUrl = "http://127.0.0.1:$apiPort"

    Write-Host "==> Probing API Liveness: $baseUrl/health/live..."
    $liveRes = Invoke-HttpRequestWithRetry -Url "$baseUrl/health/live" -MaxAttempts 30 -DelaySeconds 2
    if (-not $liveRes.Success) {
        throw "API failed liveness check after timeout ($baseUrl/health/live)."
    }
    Write-Host "[OK] Liveness OK: $($liveRes.Content)"

    Write-Host "==> Probing API Readiness: $baseUrl/health/ready..."
    $readyRes = Invoke-HttpRequestWithRetry -Url "$baseUrl/health/ready" -MaxAttempts 30 -DelaySeconds 2
    if (-not $readyRes.Success) {
        throw "API failed readiness check after timeout ($baseUrl/health/ready)."
    }
    Write-Host "[OK] Readiness OK: $($readyRes.Content)"

    if ($Mode -eq 'Aspire') {
        # Verify Aspire PostgreSQL container image digest and label mechanically
        Refresh-OwnedProcessTree
        $aspireContainersRaw = (Invoke-BoundedNativeCommand -Command "docker" -Arguments @("ps", "-a", "--no-trunc", "-q") -TimeoutMs 30000).Stdout
        $aspireContainers = if ($aspireContainersRaw) {
            @($aspireContainersRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        } else { @() }

        $verifiedPostgresImage = $false
        foreach ($cId in $aspireContainers) {
            $inspRes = Invoke-BoundedNativeCommand -Command "docker" -Arguments @("inspect", $cId) -TimeoutMs 30000
            $inspectJson = $inspRes.Stdout | ConvertFrom-Json
            if ($inspectJson -and $inspectJson.Count -gt 0) {
                $item = $inspectJson[0]
                $labels = Get-ObjectProperty (Get-ObjectProperty $item 'Config') 'Labels'
                $runLabel = if ($labels) { Get-ObjectProperty $labels 'dxos.run.id' } else { $null }
                $creatorPidStr = if ($labels) { Get-ObjectProperty $labels 'com.microsoft.developer.usvc-dev.creatorProcessId' } else { $null }
                $isTaskOwned = ($runLabel -eq $runId) -or ($creatorPidStr -and $allOwnedPids.Contains([int]$creatorPidStr))
                if ($isTaskOwned) {
                    if ($creatorPidStr) {
                        $allOwnedPids.Add([int]$creatorPidStr) | Out-Null
                        Refresh-OwnedProcessTree
                    }
                    $img = Get-ObjectProperty (Get-ObjectProperty $item 'Config') 'Image'
                    Write-Host "  [Aspire Container $cId] Image: $img | RunLabel: $runLabel | CreatorPID: $creatorPidStr"
                    if ($img -match 'postgres:18\.4-alpine@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15') {
                        $verifiedPostgresImage = $true
                    }
                }
            }
        }
        if (-not $verifiedPostgresImage) {
            throw "Aspire PostgreSQL container did not match expected pinned digest 'postgres:18.4-alpine@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15'"
        }
        Write-Host "[OK] Aspire PostgreSQL image digest mechanically verified."
    }

    Write-Host "==> Invoking Elsa Workflow Smoke: $baseUrl/smoke/workflow..."
    $corrId = "smoke-corr-$runId"
    $smokeRes = Invoke-HttpRequestWithRetry -Url "$baseUrl/smoke/workflow?correlationId=$corrId" -Method 'POST' -MaxAttempts 10 -DelaySeconds 1
    if (-not $smokeRes.Success) {
        throw "Elsa smoke workflow endpoint failed or timed out. StatusCode=$($smokeRes.StatusCode), Content=$($smokeRes.Content)"
    }

    $smokeJson = $smokeRes.Content | ConvertFrom-Json
    $instanceId = Get-ObjectProperty $smokeJson 'workflowInstanceId'
    if (-not $instanceId) {
        throw "Smoke response did not include a valid workflowInstanceId. Content: $($smokeRes.Content)"
    }
    if ($instanceId -notmatch '^[a-zA-Z0-9_-]{10,64}$') {
        throw "Workflow instance ID '$instanceId' is not in expected alphanumeric identifier format."
    }
    $wStatus = Get-ObjectProperty $smokeJson 'workflowStatus'
    $wSubStatus = Get-ObjectProperty $smokeJson 'workflowSubStatus'
    if ($wStatus -ne 'Finished' -or $wSubStatus -ne 'Finished') {
        throw "Smoke response status was '$wStatus' / '$wSubStatus', expected 'Finished' / 'Finished'."
    }
    $wOutput = Get-ObjectProperty $smokeJson 'output'
    if ($wOutput -ne 'DXOS_SMOKE_OK') {
        throw "Smoke response output was '$wOutput', expected 'DXOS_SMOKE_OK'."
    }
    $wCorr = Get-ObjectProperty $smokeJson 'correlationId'
    $wEchoed = Get-ObjectProperty $smokeJson 'echoedCorrelationId'
    if ($wCorr -ne $corrId -or $wEchoed -ne $corrId) {
        throw "Smoke response correlationId '$wCorr' or echoed '$wEchoed' did not match expected '$corrId'."
    }

    Write-Host "[OK] Elsa Smoke Workflow Succeeded!"
    Write-Host "  - Workflow Instance ID: $instanceId"
    Write-Host "  - Terminal Status:      $wStatus ($wSubStatus)"
    Write-Host "  - Output:               $wOutput"
    Write-Host "  - Echoed Correlation:   $wEchoed"

    if ($IncludeNegativeTest -and $Mode -eq 'Compose') {
        Write-Host "==> Executing Negative Dependency Test (stopping Postgres)..."
        $composeEnv = @{
            "COMPOSE_PROJECT_NAME" = $projectName
            "POSTGRES_PORT" = "$postgresPort"
            "API_PORT" = "$apiPort"
            "POSTGRES_DB" = "dxos"
            "POSTGRES_USER" = "dxos"
            "POSTGRES_PASSWORD" = $disposablePassword
        }
        Invoke-BoundedNativeCommand -Command "docker" -Arguments @("compose", "-p", $projectName, "-f", "compose.yaml", "stop", "postgres") -TimeoutMs 30000 -EnvironmentVariables $composeEnv

        Write-Host "==> Verifying Liveness remains healthy when DB is down..."
        $negLive = Invoke-HttpRequestWithRetry -Url "$baseUrl/health/live" -MaxAttempts 5 -DelaySeconds 1
        if (-not $negLive.Success) {
            throw "Negative test failure: Liveness must remain healthy when DB is down."
        }
        Write-Host "[OK] Negative test passed: Liveness is Healthy."

        Write-Host "==> Verifying Readiness becomes unhealthy (503) when DB is down..."
        $negReady = Invoke-HttpRequestWithRetry -Url "$baseUrl/health/ready" -MaxAttempts 5 -DelaySeconds 1 -ExpectedStatusCodes @(503)
        if (-not $negReady.Success) {
            throw "Negative test failure: Readiness must return 503 when DB is down."
        }
        Write-Host "[OK] Negative test passed: Readiness returned 503 Service Unavailable."

        Write-Host "==> Verifying Workflow Smoke fails (503) when DB is down..."
        $negSmoke = Invoke-HttpRequestWithRetry -Url "$baseUrl/smoke/workflow" -Method 'POST' -MaxAttempts 3 -DelaySeconds 1 -ExpectedStatusCodes @(503, 500)
        if (-not $negSmoke.Success) {
            throw "Negative test failure: Workflow smoke must fail when DB is down."
        }
        Write-Host "[OK] Negative test passed: Smoke workflow failed fast on unavailable DB."
    }

    $overallSuccess = $true
}
catch {
    $smokeFailure = $_
    Write-Host "Runtime smoke encountered error: $($smokeFailure.ToString())" -ForegroundColor Red
    $overallSuccess = $false
}
finally {
    Write-Host "==> Cleaning up task-owned resources for run $runId..."
    Refresh-OwnedProcessTree

    if ($Mode -eq 'Compose') {
        try {
            $composeEnv = @{
                "COMPOSE_PROJECT_NAME" = $projectName
                "POSTGRES_PORT" = "$postgresPort"
                "API_PORT" = "$apiPort"
                "POSTGRES_DB" = "dxos"
                "POSTGRES_USER" = "dxos"
                "POSTGRES_PASSWORD" = $disposablePassword
            }
            Invoke-BoundedNativeCommand -Command "docker" -Arguments @("compose", "-p", $projectName, "-f", "compose.yaml", "down", "-v", "--remove-orphans") -TimeoutMs 60000 -EnvironmentVariables $composeEnv
        }
        catch {
            $cleanupErrors.Add("Failed to execute docker compose down: $_")
        }
    }
    elseif ($Mode -eq 'Aspire') {
        # 1. Terminate AppHost process tree (Windows taskkill / Linux kill)
        Refresh-OwnedProcessTree
        foreach ($ownedPid in @($allOwnedPids)) {
            try {
                Stop-ProcessTreeById -ProcessId $ownedPid
            } catch {
                $cleanupErrors.Add("Failed to terminate owned process $ownedPid`: $_")
            }
        }
        foreach ($proc in $ownedProcesses) {
            try {
                if (-not $proc.HasExited) {
                    $proc.Kill()
                }
            } catch { }
            try { [void]$proc.WaitForExit(5000) } catch { }
        }
        if (-not $overallSuccess) {
            Write-AspireProcessLogs -Reason "cleanup after failure"
        }

        # 2. Identify and remove ONLY owned containers (satisfies BOTH: not in pre-existing snapshot AND has proven ownership via dxos.run.id or creatorProcessId in owned PIDs)
        try {
            $postContainersRaw = (Invoke-BoundedNativeCommand -Command "docker" -Arguments @("ps", "-a", "--no-trunc", "-q") -TimeoutMs 30000).Stdout
            $postContainers = if ($postContainersRaw) {
                @($postContainersRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            } else { @() }

            $candidateContainers = $postContainers | Where-Object { $preExistingContainers -notcontains $_ }
            foreach ($cId in $candidateContainers) {
                $inspRes = Invoke-BoundedNativeCommand -Command "docker" -Arguments @("inspect", $cId) -TimeoutMs 30000
                $inspectJson = $inspRes.Stdout | ConvertFrom-Json
                if ($inspectJson -and $inspectJson.Count -gt 0) {
                    $item = $inspectJson[0]
                    $labels = Get-ObjectProperty (Get-ObjectProperty $item 'Config') 'Labels'
                    $isOwned = $false
                    if ($labels) {
                        $runLabel = Get-ObjectProperty $labels 'dxos.run.id'
                        if ($runLabel -eq $runId) {
                            $isOwned = $true
                        }
                        $creatorPidStr = Get-ObjectProperty $labels 'com.microsoft.developer.usvc-dev.creatorProcessId'
                        if ($creatorPidStr) {
                            $creatorPid = [int]$creatorPidStr
                            if ($allOwnedPids.Contains($creatorPid)) {
                                $isOwned = $true
                            }
                        }
                    }

                    if ($isOwned) {
                        # Record mounts for volume cleanup
                        $mounts = Get-ObjectProperty $item 'Mounts'
                        if ($mounts) {
                            foreach ($m in $mounts) {
                                $mType = Get-ObjectProperty $m 'Type'
                                $mName = Get-ObjectProperty $m 'Name'
                                if ($mType -eq 'volume' -and $mName) {
                                    $ownedVolumeNames.Add($mName) | Out-Null
                                }
                            }
                        }

                        Write-Host "  -> Removing owned Aspire container $cId..."
                        Invoke-BoundedNativeCommand -Command "docker" -Arguments @("stop", $cId) -TimeoutMs 30000
                        Invoke-BoundedNativeCommand -Command "docker" -Arguments @("rm", "-v", $cId) -TimeoutMs 30000
                    } else {
                        Write-Host "  -> Container $cId appeared after snapshot but does not match owned process tree or run ID. Leaving untouched."
                        $aspireDevName = if ($labels) { Get-ObjectProperty $labels 'com.microsoft.developer.usvc-dev.name' } else { $null }
                        if ($aspireDevName) {
                            $cleanupErrors.Add("Unproven residue container $cId ($aspireDevName) detected without verified run ownership.")
                        }
                    }
                }
            }
        }
        catch {
            $cleanupErrors.Add("Failed during owned Aspire container cleanup: $_")
        }

        # 3. Identify and remove ONLY owned networks (satisfies BOTH: not in pre-existing snapshot AND matches owned Aspire creator process or run ID)
        try {
            $postNetworksRaw = (Invoke-BoundedNativeCommand -Command "docker" -Arguments @("network", "ls", "--no-trunc", "-q") -TimeoutMs 30000).Stdout
            $postNetworks = if ($postNetworksRaw) {
                @($postNetworksRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            } else { @() }

            $candidateNetworks = $postNetworks | Where-Object { $preExistingNetworks -notcontains $_ }
            foreach ($nId in $candidateNetworks) {
                $inspRes = Invoke-BoundedNativeCommand -Command "docker" -Arguments @("network", "inspect", $nId) -TimeoutMs 30000
                $inspectJson = $inspRes.Stdout | ConvertFrom-Json
                if ($inspectJson -and $inspectJson.Count -gt 0) {
                    $item = $inspectJson[0]
                    $labels = Get-ObjectProperty $item 'Labels'
                    $netName = Get-ObjectProperty $item 'Name'
                    $isOwned = $false
                    if ($labels) {
                        $runLabel = Get-ObjectProperty $labels 'dxos.run.id'
                        if ($runLabel -eq $runId) {
                            $isOwned = $true
                        }
                        $creatorPidStr = Get-ObjectProperty $labels 'com.microsoft.developer.usvc-dev.creatorProcessId'
                        if ($creatorPidStr) {
                            $creatorPid = [int]$creatorPidStr
                            if ($allOwnedPids.Contains($creatorPid)) {
                                $isOwned = $true
                            }
                        }
                    }

                    if ($isOwned) {
                        Write-Host "  -> Removing owned Aspire network $nId ($netName)..."
                        Invoke-BoundedNativeCommand -Command "docker" -Arguments @("network", "rm", $nId) -TimeoutMs 30000
                    } else {
                        Write-Host "  -> Network $nId appeared after snapshot but does not match owned process tree or run ID. Leaving untouched."
                        if ($netName -match '^aspire-session-network-.*-DXOS$') {
                            $cleanupErrors.Add("Unproven residue network $nId ($netName) detected without verified run ownership.")
                        }
                    }
                }
            }
        }
        catch {
            $cleanupErrors.Add("Failed during owned Aspire network cleanup: $_")
        }

        # 4. Identify and remove ONLY owned volumes
        try {
            $postVolumesRaw = (Invoke-BoundedNativeCommand -Command "docker" -Arguments @("volume", "ls", "-q") -TimeoutMs 30000).Stdout
            $postVolumes = if ($postVolumesRaw) {
                @($postVolumesRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            } else { @() }

            $candidateVolumes = $postVolumes | Where-Object { $preExistingVolumes -notcontains $_ }
            foreach ($vId in $candidateVolumes) {
                $inspRes = Invoke-BoundedNativeCommand -Command "docker" -Arguments @("volume", "inspect", $vId) -TimeoutMs 30000
                $inspectJson = $inspRes.Stdout | ConvertFrom-Json
                if ($inspectJson -and $inspectJson.Count -gt 0) {
                    $item = $inspectJson[0]
                    $labels = Get-ObjectProperty $item 'Labels'
                    $isOwned = $false
                    if ($ownedVolumeNames.Contains($vId)) {
                        $isOwned = $true
                    }
                    if ($labels) {
                        $runLabel = Get-ObjectProperty $labels 'dxos.run.id'
                        if ($runLabel -eq $runId) {
                            $isOwned = $true
                        }
                        $creatorPidStr = Get-ObjectProperty $labels 'com.microsoft.developer.usvc-dev.creatorProcessId'
                        if ($creatorPidStr -and $allOwnedPids.Contains([int]$creatorPidStr)) {
                            $isOwned = $true
                        }
                    }

                    if ($isOwned) {
                        Write-Host "  -> Removing owned Aspire volume $vId..."
                        Invoke-BoundedNativeCommand -Command "docker" -Arguments @("volume", "rm", $vId) -TimeoutMs 30000
                    } else {
                        Write-Host "  -> Volume $vId appeared after snapshot but does not match owned process tree or run ID. Leaving untouched."
                    }
                }
            }
        }
        catch {
            $cleanupErrors.Add("Failed during owned Aspire volume cleanup: $_")
        }
    }

    # Verify that all pre-existing containers, networks, and volumes are still present and untouched
    $finalContainersRaw = (Invoke-BoundedNativeCommand -Command "docker" -Arguments @("ps", "-a", "--no-trunc", "-q") -TimeoutMs 30000).Stdout
    $finalContainers = if ($finalContainersRaw) {
        @($finalContainersRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    } else { @() }

    foreach ($preId in $preExistingContainers) {
        if ($finalContainers -notcontains $preId) {
            $cleanupErrors.Add("Pre-existing container $preId was modified or deleted during runtime smoke!")
        }
    }

    $finalNetworksRaw = (Invoke-BoundedNativeCommand -Command "docker" -Arguments @("network", "ls", "--no-trunc", "-q") -TimeoutMs 30000).Stdout
    $finalNetworks = if ($finalNetworksRaw) {
        @($finalNetworksRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    } else { @() }

    foreach ($preId in $preExistingNetworks) {
        if ($finalNetworks -notcontains $preId) {
            $cleanupErrors.Add("Pre-existing network $preId was modified or deleted during runtime smoke!")
        }
    }

    $finalVolumesRaw = (Invoke-BoundedNativeCommand -Command "docker" -Arguments @("volume", "ls", "-q") -TimeoutMs 30000).Stdout
    $finalVolumes = if ($finalVolumesRaw) {
        @($finalVolumesRaw -split '\r?\n' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    } else { @() }

    foreach ($preId in $preExistingVolumes) {
        if ($finalVolumes -notcontains $preId) {
            $cleanupErrors.Add("Pre-existing volume $preId was modified or deleted during runtime smoke!")
        }
    }

    if ($cleanupErrors.Count -gt 0) {
        Write-Error "Cleanup failed with errors: $($cleanupErrors -join '; ')"
        exit 1
    }

    if (-not $overallSuccess) {
        Write-Error "Runtime smoke failed: $($smokeFailure.ToString())"
        exit 1
    }

    Write-Host "[OK] Runtime smoke completed successfully and all task-owned resources were cleaned up."
    exit 0
}

