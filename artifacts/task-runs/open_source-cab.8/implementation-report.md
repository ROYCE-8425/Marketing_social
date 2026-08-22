# BR001-R7 Implementation Report: CI, Security Scanning, SBOM, and OSS Compliance

## Metadata

- **Task**: BR001-R7 (`open_source-cab.8`)
- **Specification**: `openspec/changes/bootstrap-remediation-001/tasks.md` (R7.1–R7.4 remain `[ ]`)
- **HEAD**: `c2b3390d18068c805a8ad31bf0173afc2ba63b5b`
- **Date**: 2026-08-21
- **Status**: Ready for independent Codex review of Review-3 blockers
- **LIVE_CI_STATUS**: PENDING_OWNER_AUTHORIZED_POST_PASS_PUBLICATION
- **Full execution count this session**: 1
- **Commit/push/R8**: none

## What this session changed

This was a narrow Review-3 fix, not a restart of R7 and not product work.

1. **Run-owned security evidence + atomic summary**
   - `scripts/check.ps1` writes Full evidence under `artifacts/quality-gate/run-{RunId}/`.
   - Scanners write reports and `{report}.meta.json` sidecars bound to that RunId.
   - `scripts/generate-security-summary.ps1` requires RunId, rejects cross-run/stale sidecars, validates SBOM duplicates before committed-hash equality, redacts machine paths, normalizes Grype `sha256:` checksums, and atomically replaces canonical + evidence summaries with rollback if the second destination fails.
   - `-ValidateExisting` rejects a clean report whose hash no longer matches the recorded summary.

2. **Exact SBOM duplicate validation**
   - Canonical key: purl (case-normalized) if present, else `type:name@version`.
   - Production validator is `generate-security-summary.ps1`; a duplicate-component fixture calls that script and fails.

3. **CI YAML parse vs global.json**
   - `.github/workflows/ci.yaml` already pins SDK `10.0.302`.
   - `scripts/verify-ci-parity.ps1` now parses the workflow YAML structurally, compares `dotnet-version` to `global.json`, requires SHA-pinned actions, Ubuntu Full, OpenSpec 1.8.0, no continue-on-error/secrets/deploy, and no Windows-only `.cmd/.exe` Full commands.
   - Live GitHub Actions was not run. Origin is `https://github.com/ROYCE-8425/open_source.git`, which is not an authorized DX-OS public remote.

4. **TAR extraction fixtures**
   - `setup-security-tools.ps1 -ValidateTarArchive` runs the production `Validate-TarArchiveSafely` function.
   - Real malicious TAR fixtures (`../`, absolute path, symlink) are generated with Python `tarfile` and rejected before extraction.

5. **Exact OSS identity sets**
   - NuGet lockfiles remain bidirectional (228 packages; Project references excluded).
   - Container images, security tools, and services now compare exact identity including version, digest/license, and tool acquisition/executable SHA-256 for Windows and Linux.
   - Inventory records for the four scanners now include those hashes.

6. **SDK pin in Full**
   - `check.ps1` accepts only `10.0.302` or later `10.0.3xx` (`latestPatch`). `10.0.100` is rejected.

## Exact changed files

- `scripts/check.ps1`
- `scripts/generate-security-summary.ps1`
- `scripts/run-security-scan.ps1`
- `scripts/setup-security-tools.ps1`
- `scripts/verify-check-contract.ps1`
- `scripts/verify-ci-parity.ps1`
- `scripts/verify-oss-compliance.ps1`
- `artifacts/oss-inventory.json`
- `artifacts/sbom.cdx.json`
- `artifacts/security/security-summary.json`
- `artifacts/task-runs/open_source-cab.8/implementation-report.md`
- `artifacts/task-runs/open_source-cab.8/verification.md`

## Frozen Full run (exactly one)

- **Command**: `powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1 -Profile Full`
- **Start**: `2026-08-21T11:14:30.2286615+07:00`
- **End**: `2026-08-21T11:16:27.0063674+07:00`
- **Wall duration**: 116.78 s
- **Exit code**: 0
- **overallResult**: PASS
- **Evidence**: `artifacts/quality-gate/run-ab4ac058-718f-4e78-add8-b3d3eb97f772/evidence-Full-ab4ac058-718f-4e78-add8-b3d3eb97f772.json`
- **Evidence SHA-256**: `150749EE06BA937E077557FAE8556DD452334BDC5256A38785717AEC813528FF`
- **Gates**: 26 (25 READY + 1 NOT_APPLICABLE). Zero R8 gates.

## Tool identity (from this Full summary)

| Tool | Version | License | Executable SHA-256 (windows-x64) |
|---|---|---|---|
| Gitleaks | 8.30.0 | MIT | `9d08e3f5cfb35a98f230b97bcda24f8d3fc66363c91868ffc98dac0afebdcb72` |
| Trivy | 0.72.0 | Apache-2.0 | `5c233d1514d6fd91f7a4f834beb92070f8a9793c71801f7f2149a7b30f90b821` |
| Syft | 1.50.0 | Apache-2.0 | `98a3779f229905fec96e16018adf27ed7a93adc2869d17e6fb21961eb501d398` |
| Grype | 0.116.1 | Apache-2.0 | `0ab5d366118e20784222ce13c0f696e5997ae42bbcea80420d264a9c42098b57` |

## Scanner databases (this Full run)

- **Trivy**: DB version 2; UpdatedAt `2026-08-21 01:31:14.037676497 +0000 UTC`; Check bundle `sha256:1583562f8b90ed2a071b99f0e5ffff6b57e4ceb6ca3e4796577b4e6a339eb74c`
- **Grype**: schema `v6.1.9`; built `2026-08-20T06:17:08Z`; checksum `sha256:39324b8b1e4ec165a873541afb91a9c1f06b090354d5461f2e7da15569dbd0fd`; status `valid`

## Deliverable / SBOM / OSS

- Image: `dxos-api:0.1.0-spike`
- Image digest: `sha256:acb330778467aafa528f86e2ad7e12bf5638bd82159b98d0385b4d98ac477812`
- SBOM: CycloneDX 1.7, 122 components, 0 canonical duplicates
- SBOM SHA-256: `5A3A5F388CDF6031D55DF110FF77D721C2E61614B56F380539ACF40FEA840DF1`
- Security summary SHA-256: `685EF0994AB45FFC926A2FF61E82B243524CA854A04217E062A44F6664647B79`
- OSS inventory SHA-256: `AA2069DE842D0F8C57B0387F7DBA1105C6DE17C69D44EBDF20D64711C0BDA50D`
- Packages: 228 exact (13 direct + 215 transitive), 0 missing/extra
- Images: 3 exact (name|version|digest|license)
- Tools: 4 exact (name|version|license|archive/exe hashes)
- Services: 2 exact (name|version|license|source)
- reusedSource: 0

## Unresolved limitations

- Authorized GitHub Actions run ID, job conclusion, and downloaded artifact hashes do not exist yet. R7.3 live CI remains pending owner-authorized publication on a DX-OS-owned public remote.
- Beads CLI (`bd.cmd` / `bd.js`) returned exit `-1` with empty stdout from this path; OpenSpec R7 checkboxes were left `[ ]` and the issue was not mutated.
- `verify-security-canaries.ps1` was not re-executed in this session; Full ran the real scanners once.
