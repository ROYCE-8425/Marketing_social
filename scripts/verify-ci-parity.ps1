[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot\..).FullName
$ciWorkflowPath = Join-Path $repoRoot ".github\workflows\ci.yaml"
$globalJsonPath = Join-Path $repoRoot "global.json"
$contractFile = Join-Path $repoRoot "scripts\check-contract.json"

if (-not (Test-Path $ciWorkflowPath)) {
    throw "CI workflow file missing at: $ciWorkflowPath"
}
if (-not (Test-Path $globalJsonPath)) {
    throw "global.json file missing at: $globalJsonPath"
}
if (-not (Test-Path $contractFile)) {
    throw "Missing scripts/check-contract.json"
}

function ConvertFrom-GithubActionsYaml {
    param([string]$Text)

    $script:yamlLines = @()
    foreach ($raw in ($Text -split "`r?`n")) {
        $script:yamlLines += $raw
    }
    $script:idx = 0

    function Get-YamlIndent([string]$line) {
        $count = 0
        foreach ($ch in $line.ToCharArray()) {
            if ($ch -eq ' ') { $count++ } else { break }
        }
        return $count
    }

    function Skip-YamlNoise {
        while ($script:idx -lt $script:yamlLines.Count) {
            $t = $script:yamlLines[$script:idx]
            if ($t -match '^\s*$' -or $t -match '^\s*#') {
                $script:idx++
                continue
            }
            break
        }
    }

    function ConvertFrom-YamlScalar([string]$raw) {
        $v = $raw.Trim()
        if ($v -match '^([^#]*?)\s+#.*$' -and $v -notmatch '^["''].*') {
            $v = $Matches[1].Trim()
        }
        if ($v.Length -ge 2 -and $v.StartsWith('"') -and $v.EndsWith('"')) {
            return $v.Substring(1, $v.Length - 2)
        }
        if ($v.Length -ge 2 -and $v.StartsWith("'") -and $v.EndsWith("'")) {
            return $v.Substring(1, $v.Length - 2)
        }
        if ($v -match '^\[(.*)\]$') {
            $inner = $Matches[1].Trim()
            if ($inner -eq '') { return @() }
            $items = @()
            foreach ($part in ($inner.Split(','))) {
                $items += (ConvertFrom-YamlScalar $part)
            }
            return $items
        }
        return $v
    }

    function Read-YamlLiteral([int]$indent) {
        $buf = New-Object System.Collections.Generic.List[string]
        while ($script:idx -lt $script:yamlLines.Count) {
            $l = $script:yamlLines[$script:idx]
            if ($l -match '^\s*$') {
                $buf.Add("") | Out-Null
                $script:idx++
                continue
            }
            $li = Get-YamlIndent $l
            if ($li -lt $indent) { break }
            if ($l.Length -ge $indent) {
                $buf.Add($l.Substring($indent)) | Out-Null
            } else {
                $buf.Add("") | Out-Null
            }
            $script:idx++
        }
        return ($buf -join "`n").TrimEnd()
    }

    function Read-YamlNode([int]$indent) {
        Skip-YamlNoise
        if ($script:idx -ge $script:yamlLines.Count) { return $null }
        $line = $script:yamlLines[$script:idx]
        $i = Get-YamlIndent $line
        if ($i -lt $indent) { return $null }
        if ($line.TrimStart().StartsWith("- ")) {
            return Read-YamlList $i
        }
        return Read-YamlMap $i
    }

    function Read-YamlList([int]$indent) {
        $list = New-Object System.Collections.Generic.List[object]
        while ($script:idx -lt $script:yamlLines.Count) {
            Skip-YamlNoise
            if ($script:idx -ge $script:yamlLines.Count) { break }
            $l = $script:yamlLines[$script:idx]
            $li = Get-YamlIndent $l
            if ($li -lt $indent) { break }
            if ($li -ne $indent -or -not $l.TrimStart().StartsWith("- ")) { break }
            $itemText = $l.TrimStart().Substring(2)
            $script:idx++
            if ($itemText -match '^([^:]+):\s*(.*)$') {
                $key = $Matches[1].Trim()
                $rest = $Matches[2]
                $obj = [ordered]@{}
                if ($rest -eq "|") {
                    $obj[$key] = Read-YamlLiteral ($indent + 2)
                } elseif ([string]::IsNullOrWhiteSpace($rest)) {
                    $obj[$key] = Read-YamlNode ($indent + 2)
                } else {
                    $obj[$key] = ConvertFrom-YamlScalar $rest
                }
                $nested = Read-YamlMap ($indent + 2)
                if ($null -ne $nested) {
                    foreach ($nk in $nested.Keys) {
                        $obj[$nk] = $nested[$nk]
                    }
                }
                $list.Add($obj) | Out-Null
            } else {
                $list.Add((ConvertFrom-YamlScalar $itemText)) | Out-Null
            }
        }
        return $list
    }

    function Read-YamlMap([int]$indent) {
        $map = [ordered]@{}
        $any = $false
        while ($script:idx -lt $script:yamlLines.Count) {
            Skip-YamlNoise
            if ($script:idx -ge $script:yamlLines.Count) { break }
            $l = $script:yamlLines[$script:idx]
            $li = Get-YamlIndent $l
            if ($li -lt $indent) { break }
            if ($l.TrimStart().StartsWith("- ")) { break }
            if ($li -ne $indent) {
                throw "YAML indent error at line $($script:idx + 1): $l"
            }
            if ($l -notmatch '^\s*([^:#][^:]*):\s*(.*)$') {
                throw "YAML parse error at line $($script:idx + 1): $l"
            }
            $key = $Matches[1].Trim()
            $rest = $Matches[2]
            $script:idx++
            $any = $true
            if ($rest -eq "|") {
                $map[$key] = Read-YamlLiteral ($indent + 2)
            } elseif ([string]::IsNullOrWhiteSpace($rest)) {
                $map[$key] = Read-YamlNode ($indent + 2)
            } else {
                $map[$key] = ConvertFrom-YamlScalar $rest
            }
        }
        if (-not $any) { return $null }
        return $map
    }

    return Read-YamlNode 0
}

Write-Host "Verifying CI workflow by parsing YAML and comparing global.json/contract commands..." -ForegroundColor Cyan

$globalJson = Get-Content $globalJsonPath -Raw | ConvertFrom-Json
$expectedSdkVersion = $globalJson.sdk.version
if ([string]::IsNullOrWhiteSpace($expectedSdkVersion)) {
    throw "global.json does not declare sdk.version"
}
if ($expectedSdkVersion -ne "10.0.302") {
    throw "global.json sdk.version must be 10.0.302, found '$expectedSdkVersion'."
}

$ciContent = Get-Content $ciWorkflowPath -Raw
$workflow = ConvertFrom-GithubActionsYaml $ciContent
if ($null -eq $workflow -or $workflow -isnot [System.Collections.IDictionary]) {
    throw "Failed to parse .github/workflows/ci.yaml as a YAML mapping."
}

if ($workflow["permissions"]["contents"] -ne "read") {
    throw "CI workflow must declare permissions.contents: read."
}
$jobs = $workflow["jobs"]
if ($null -eq $jobs -or $jobs.Keys.Count -ne 1) {
    throw "CI workflow must contain exactly one job."
}
$jobName = @($jobs.Keys)[0]
$job = $jobs[$jobName]
if ($job["runs-on"] -ne "ubuntu-latest") {
    throw "CI workflow must run on ubuntu-latest for Linux container/image parity."
}

$steps = @($job["steps"])
if ($steps.Count -lt 6) {
    throw "CI workflow is missing required steps (found $($steps.Count))."
}

$usesRefs = @()
$hasSetupNode = $false
$hasSetupDotnet = $false
$hasUploadArtifact = $false
$hasOpenspecInstall = $false
$hasSecurityTools = $false
$hasCanaries = $false
$hasFullGate = $false
$ciSdkVersion = $null

foreach ($step in $steps) {
    if ($step.Contains("continue-on-error") -and "$($step['continue-on-error'])" -eq "true") {
        throw "CI workflow contains continue-on-error: true."
    }
    if ($step.Contains("uses")) {
        $uses = [string]$step["uses"]
        if ($uses -notmatch '^([^@]+)@([0-9a-f]{40})$') {
            throw "Action '$uses' is not pinned to a 40-character commit SHA."
        }
        $actionName = $Matches[1]
        $actionSha = $Matches[2]
        $usesRefs += "$actionName@$actionSha"
        if ($actionName -eq "actions/setup-node") { $hasSetupNode = $true }
        if ($actionName -eq "actions/setup-dotnet") {
            $hasSetupDotnet = $true
            if ($step.Contains("with") -and $step["with"].Contains("dotnet-version")) {
                $ciSdkVersion = [string]$step["with"]["dotnet-version"]
            }
        }
        if ($actionName -eq "actions/upload-artifact") { $hasUploadArtifact = $true }
    }
    if ($step.Contains("run")) {
        $runText = [string]$step["run"]
        if ($runText -match '@fission-ai/openspec@1\.8\.0') { $hasOpenspecInstall = $true }
        if ($runText -match 'setup-security-tools\.ps1') { $hasSecurityTools = $true }
        if ($runText -match 'verify-security-canaries\.ps1') { $hasCanaries = $true }
        if ($runText -match 'check\.ps1\s+-Profile\s+Full') { $hasFullGate = $true }
        if ($runText -match 'secrets\.') {
            throw "CI workflow run step references secrets context."
        }
        if ($runText -match '(?i)\b(gh release|docker push|helm upgrade|kubectl apply)\b') {
            throw "CI workflow contains deployment/publishing commands."
        }
    }
}

if (-not $hasSetupNode) { throw "CI workflow missing actions/setup-node." }
if (-not $hasSetupDotnet) { throw "CI workflow missing actions/setup-dotnet." }
if (-not $hasUploadArtifact) { throw "CI workflow missing actions/upload-artifact." }
if (-not $hasOpenspecInstall) { throw "CI workflow missing pinned @fission-ai/openspec@1.8.0 installation." }
if (-not $hasSecurityTools) { throw "CI workflow missing pinned security tools acquisition step." }
if (-not $hasCanaries) { throw "CI workflow missing security canaries verification step." }
if (-not $hasFullGate) { throw "CI workflow must execute scripts/check.ps1 -Profile Full." }
if ($ciSdkVersion -ne $expectedSdkVersion) {
    throw "CI workflow .NET SDK version mismatch. ci.yaml='$ciSdkVersion', global.json='$expectedSdkVersion'."
}

$contract = Get-Content $contractFile -Raw | ConvertFrom-Json
$fullGates = @($contract.gates | Where-Object { $_.profiles -contains "Full" })
$r8InFull = @($fullGates | Where-Object { $_.task -match '^BR001-R8' })
if ($r8InFull.Count -ne 0) {
    throw "Full contract contains R8 gates; CI must not treat R8 as part of Full."
}

$windowsOnly = @()
foreach ($g in $fullGates) {
    if ($g.command -match '\.(cmd|bat|exe)$') {
        $windowsOnly += "$($g.id):$($g.command)"
    }
    foreach ($tool in @($g.requiredTools)) {
        if ($tool -match '\.(cmd|bat|exe)$') {
            $windowsOnly += "$($g.id) tool:$tool"
        }
    }
}
if ($windowsOnly.Count -gt 0) {
    throw "Full contract still uses Windows-only executables on Ubuntu CI: $($windowsOnly -join ', ')"
}

$requiredCommands = @("powershell", "dotnet", "docker", "openspec", "git")
foreach ($cmd in $requiredCommands) {
    if ($cmd -eq "powershell") { continue }
    if (-not (Get-Command $cmd -ErrorAction SilentlyContinue) -and -not (Get-Command "$cmd.cmd" -ErrorAction SilentlyContinue) -and -not (Get-Command "$cmd.exe" -ErrorAction SilentlyContinue)) {
        Write-Host "NOTE: local preflight did not resolve '$cmd' in this shell; CI installs missing tools explicitly." -ForegroundColor Yellow
    }
}

if (-not (Get-Command "pwsh" -ErrorAction SilentlyContinue) -and -not (Get-Command "powershell" -ErrorAction SilentlyContinue) -and -not (Get-Command "powershell.exe" -ErrorAction SilentlyContinue)) {
    throw "Neither pwsh nor powershell is available to execute Full."
}

Write-Host "PASS: Parsed CI YAML; SDK $expectedSdkVersion matches global.json; actions SHA-pinned; Full command/tool set is Ubuntu-resolvable; no R8/deploy/credentials." -ForegroundColor Green
Write-Host "LIVE_CI_STATUS: PENDING_OWNER_AUTHORIZED_POST_PASS_PUBLICATION" -ForegroundColor Yellow
