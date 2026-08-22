# BR001-R7 Verification

Date: 2026-08-21  
HEAD: `c2b3390d18068c805a8ad31bf0173afc2ba63b5b`  
Full runs this session: **1**

## Targeted checks (before Full)

| # | Command | Runs | Exit | Result |
|---|---|---|---|---|
| 1 | PowerShell AST parse of 7 changed scripts | 1 | 0 | PASS |
| 2 | `scripts/verify-ci-parity.ps1` (YAML parse + SDK equality) | 1 | 0 | PASS |
| 3 | `scripts/verify-oss-compliance.ps1 -Check Inventory` | 1 | 0 | PASS (228/3/4/2 exact) |
| 4 | `scripts/verify-check-contract.ps1` real fixtures | 2 | 0 | PASS (second run after NOT_IMPLEMENTED profile registration fix) |
| 5 | `git diff --check` | 1 | 0 | PASS |
| 6 | `openspec.cmd validate bootstrap-remediation-001 --type change --strict --no-interactive` | 1 | 0 | PASS |
| 7 | `bd.cmd show open_source-cab.8 --json` | 1 | -1 | BLOCKED in this shell (empty stdout); issue not mutated |

Foundation / Runtime were not run as standalone profiles.

## Real negative fixtures (production validators)

| Fixture | Production entrypoint | Observed failure |
|---|---|---|
| Missing tool preflight | `check.ps1` | missing required tool, subsequent gates blocked |
| Clean-but-stale Gitleaks hash | `generate-security-summary.ps1 -ValidateExisting` | `stale hash for 'gitleaks'` |
| TAR `../escaped.txt` | `setup-security-tools.ps1 -ValidateTarArchive` | traversal rejected; no outside file |
| TAR `/tmp/abs.txt` | same | absolute path rejected |
| TAR symlink `link -> ../outside` | same | symlink rejected |
| Missing package Elsa | `verify-oss-compliance.ps1 -Check Inventory` | missing `Elsa@3.7.1` |
| Extra package | same | extra `Bogus.Extra.Package@1.0.0` |
| Wrong Elsa version | same | missing lock identity `Elsa@3.7.1` |
| Wrong postgres digest | same | image exact-set mismatch |
| Extra container image | same | extra image identity |
| Missing trivy | same | tool exact-set missing |
| Wrong trivy archive hash | same | tool exact-set mismatch |
| Missing Google Gemini | same | service exact-set missing |
| Duplicate SBOM component | `generate-security-summary.ps1` | duplicate `pkg:nuget/cshells.abstractions@0.0.14` |
| NOT_IMPLEMENTED gate | `check.ps1` TestNotImplProfile | firstFailure = fake R8 gate, overall FAIL |

## Frozen Full (exactly once)

```
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1 -Profile Full
```

- Start: `2026-08-21T11:14:30.2286615+07:00`
- End: `2026-08-21T11:16:27.0063674+07:00`
- Duration: 116.78 s
- Exit: 0
- overallResult: PASS
- RunId: `ab4ac058-718f-4e78-add8-b3d3eb97f772`
- Evidence: `artifacts/quality-gate/run-ab4ac058-718f-4e78-add8-b3d3eb97f772/evidence-Full-ab4ac058-718f-4e78-add8-b3d3eb97f772.json`

### Gate sequence

| Gate | Status | Exit | Seconds |
|---|---|---|---|
| foundation-restore | READY | 0 | 2.01 |
| foundation-format | READY | 0 | 6.59 |
| foundation-build | READY | 0 | 5.14 |
| foundation-openspec | READY | 0 | 0.77 |
| foundation-hygiene | READY | 0 | 0.12 |
| runtime-docker-compose | READY | 0 | 0.32 |
| runtime-smoke-compose | READY | 0 | 12.46 |
| runtime-smoke-aspire | READY | 0 | 21.80 |
| runtime-unit-tests | READY | 0 | 3.86 |
| runtime-architecture-tests | READY | 0 | 4.77 |
| runtime-integration-tests | READY | 0 | 11.95 |
| runtime-e2e-tests | NOT_APPLICABLE | 0 | 0.00 |
| full-gitleaks-scan | READY | 0 | 2.21 |
| full-trivy-scan | READY | 0 | 20.22 |
| full-syft-sbom | READY | 0 | 12.55 |
| full-grype-scan | READY | 0 | 3.97 |
| full-security-summary | READY | 0 | 3.23 |
| full-governance-instructions | READY | 0 | 0.43 |
| full-governance-adr-sync | READY | 0 | 0.28 |
| full-governance-state | READY | 0 | 0.26 |
| full-ci-parity | READY | 0 | 0.43 |
| full-oss-license | READY | 0 | 0.28 |
| full-oss-attribution | READY | 0 | 0.28 |
| full-oss-inventory | READY | 0 | 0.57 |
| full-oss-service-disclosure | READY | 0 | 0.28 |
| full-identity-public | READY | 0 | 0.29 |

No R8 gate ran. R8 remains NOT_IMPLEMENTED on ReadyAudit only.

## Security-summary recorded vs actual hashes

| Artifact | Recorded | Actual | Result |
|---|---|---|---|
| gitleaks-report.json | `37517E5F3DC66819F61F5A7BB8ACE1921282415F10551D2DEFA5C3EB0985B570` | `37517E5F3DC66819F61F5A7BB8ACE1921282415F10551D2DEFA5C3EB0985B570` | MATCH |
| trivy-config-report.json | `322386194DFFFE5EAC33347BA2D48420AAED52CCDEB0496287F153C1009632E7` | same | MATCH |
| trivy-image-report.json | `FFE1869184A6377E2AAF5BE6D56F43F5D5FC3AC21FE0E99FE409B8D5EC8EA9A6` | same | MATCH |
| artifacts/sbom.cdx.json | `5A3A5F388CDF6031D55DF110FF77D721C2E61614B56F380539ACF40FEA840DF1` | same | MATCH |
| grype-report.json | `42DDEC71CF023BA2BEB58078DA1CACFFAC739932A3016D612C7B1152BA00B121` | same | MATCH |

Summary `runId` equals Full evidence `runId` (`ab4ac058-718f-4e78-add8-b3d3eb97f772`).  
Summary text contains no `C:\Users` / OneDrive / `/home/` leak. Grype checksum is `sha256:...`, not `sha256%3A`.

## SBOM

- Canonical key: purl if present, else `type:name@version`
- Components: 122
- Duplicates: 0
- Format: CycloneDX 1.7
- Metadata component: `dxos-api`

## OSS reconciliation

- Lockfile/inventory packages: 228 / 228, missing 0, extra 0, version mismatch 0
- Images: 3 / 3
- Security tools: 4 / 4 including acquisition and executable SHA-256
- Services: 2 / 2 including version + officialSource
- reusedSource: 0

## CI

- Local YAML/SDK/command parity: PASS
- Remote CI: PENDING_OWNER_AUTHORIZED_POST_PASS_PUBLICATION
- No run ID, conclusion, or downloaded artifact hashes

## Governance

- OpenSpec R7.1–R7.4: still `[ ]`
- Beads `open_source-cab.8`: left `in_progress` (CLI query failed in this shell; no close/claim mutation)
- No commit, push, remote edit, credential handling, global tool install, or R8 implementation
