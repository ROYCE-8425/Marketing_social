[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Instructions", "ADR", "State")]
    [string]$Check
)

$ErrorActionPreference = "Stop"
$repoRoot = (Get-Item $PSScriptRoot\..).FullName

switch ($Check) {
    "Instructions" {
        Write-Host "Verifying DX-OS agent instructions and rules..." -ForegroundColor Cyan
        $agentFiles = @(
            "AGENTS.md",
            ".agents/rules/00-authority.md",
            ".agents/rules/10-dotnet-architecture.md",
            ".agents/rules/20-testing.md",
            ".agents/rules/30-security.md",
            ".agents/rules/40-database.md",
            ".agents/rules/50-ai-governance.md",
            ".agents/rules/60-git.md"
        )

        foreach ($rel in $agentFiles) {
            $full = Join-Path $repoRoot $rel
            if (-not (Test-Path $full)) {
                throw "Missing required agent rule/instruction file: $rel"
            }
            $bytes = [System.IO.File]::ReadAllBytes($full)
            # Check BOM
            if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) {
                throw "UTF-8 BOM found in $rel"
            }
            # Check C0 control characters
            for ($i = 0; $i -lt $bytes.Length; $i++) {
                $b = $bytes[$i]
                if ($b -lt 0x20 -and $b -ne 0x0A -and $b -ne 0x0D) {
                    throw "Forbidden C0 character 0x{0:X2} at byte {1} in {2}" -f $b, $i, $rel
                }
            }
        }

        # Check content requirements
        $agentsMd = Get-Content (Join-Path $repoRoot "AGENTS.md") -Raw
        if ($agentsMd -match "Elsa\.sln|\./build\.sh|NUKE|test/unit/Elsa") {
            throw "Forbidden legacy Elsa command found in AGENTS.md"
        }
        if (-not ($agentsMd -match "DX-OS" -and $agentsMd -match "Apache-2\.0" -and $agentsMd -match "constitution")) {
            throw "AGENTS.md missing core DX-OS identity, license, or constitution authority requirements."
        }
        Write-Host "PASS: Agent instructions and rules verified." -ForegroundColor Green
    }

    "ADR" {
        Write-Host "Verifying Architecture Decision Records index and synchronization..." -ForegroundColor Cyan
        $adrIndex = Join-Path $repoRoot "docs\adr\README.md"
        if (-not (Test-Path $adrIndex)) {
            throw "Missing docs/adr/README.md"
        }
        $indexContent = Get-Content $adrIndex -Raw
        $expectedAdrs = @(
            "0001-dx-os-open-source-license.md",
            "0002-third-party-services-and-ai-provider-independence.md",
            "0003-independent-repository-extraction-and-identity.md",
            "0004-modular-monolith-architecture.md",
            "0005-postgresql-persistence.md",
            "0006-elsa-nuget-integration.md",
            "0007-aspire-and-docker-compose-orchestration.md"
        )

        foreach ($adr in $expectedAdrs) {
            $adrPath = Join-Path $repoRoot "docs\adr\$adr"
            if (-not (Test-Path $adrPath)) {
                throw "Missing expected ADR document: docs/adr/$adr"
            }
            if (-not ($indexContent -match [regex]::Escape($adr))) {
                throw "ADR '$adr' is not linked in docs/adr/README.md"
            }
        }
        Write-Host "PASS: All 7 bootstrap ADRs exist and are properly indexed." -ForegroundColor Green
    }

    "State" {
        Write-Host "Verifying project state and evidence index..." -ForegroundColor Cyan
        $stateFile = Join-Path $repoRoot "docs\PROJECT_STATE.md"
        $evidenceIndex = Join-Path $repoRoot "docs\EVIDENCE_INDEX.md"

        if (-not (Test-Path $stateFile)) { throw "Missing docs/PROJECT_STATE.md" }
        if (-not (Test-Path $evidenceIndex)) { throw "Missing docs/EVIDENCE_INDEX.md" }

        $stateContent = Get-Content $stateFile -Raw
        if (-not ($stateContent -match "Status\*\*:\s*NOT_READY")) {
            throw "PROJECT_STATE.md release readiness status must remain NOT_READY during bootstrap remediation."
        }
        if (-not ($stateContent -match "BR001-R[1-6]")) {
            throw "PROJECT_STATE.md missing accepted capabilities records."
        }
        Write-Host "PASS: Project state and evidence index verified." -ForegroundColor Green
    }
}
