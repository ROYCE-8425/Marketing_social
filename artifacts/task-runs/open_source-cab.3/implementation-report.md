# BR001-R4 Implementation Report: Engineering Runtime Spike (Re-review 4 Resolved)

## 1. Executive Summary
This report documents the implementation, safety hardening, and frozen verification of **BR001-R4 Engineering Runtime Spike** for `dx-os` under OpenSpec change `bootstrap-remediation-001` and Beads issue `open_source-cab.3`.

All Codex Re-review 4 requirements and defect blockers have been resolved with mechanical proofs:
- **FIX 1 (Exact Canonical Docker Snapshot Comparison)**: Pre- and post-run Docker inventories capture deterministic canonical snapshots across all containers (Id, Name, Config.Image, ImageID, State, Labels, Mounts, NetworkAttachments, HostConfig), networks (Id, Name, Driver, Scope, Internal, Attachable, Labels, IPAM), and volumes (Name, Driver, Scope, Labels, Options) with sorted dictionaries. Machine-readable comparison in `docker-comparison.json` proves 0 mutations to pre-existing resources and 0 task-owned residue.
- **FIX 2 (Real Concurrent Sentinel Safety Tests)**: `test-safety-contracts.ps1` and `smoke-runtime.ps1` implement a deterministic test-only synchronization hook (`-TestSyncDir`/`-TestSyncRunId`). Smoke captures its pre-run snapshot, signals completion, and bounded-waits while the test harness creates concurrent sentinels. The test mechanically proves that concurrent sentinels created *after* snapshot capture survive Aspire teardown untouched.
- **FIX 3 (Single Frozen 29-Step Verification Suite)**: `run-r4-verification.ps1` executed all 29 sequential steps from start to finish with exit code 0, generating fresh logs `step-01` through `step-29`, a 29-row execution matrix (Run ID `33c7c91ea2e3`), and cryptographic SHA-256 sidecars.
- **FIX 4 (Safe Aspire Teardown)**: Ownership of Aspire containers and networks strictly requires: (1) not present in pre-run snapshot, (2) verified `dxos.run.id=<runId>` label OR verified `creatorProcessId` matching the exact owned AppHost PID tree, (3) matching candidate ID, and (4) fail-closed unproven residue error handling.
- **FIX 5 (Immutable Image Digest Pinning)**: PostgreSQL in Aspire is pinned to `postgres:18.4-alpine@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15` and verified upon container startup.
- **FIX 6 (Comprehensive Hygiene & Secret Scanning)**: Step 29 strictly scans all deliverable files for UTF-8 validity, trailing whitespace, replacement characters, control characters, and secret patterns without self-matching.

---

## 2. Baseline and Working Tree State

- **Repository Root**: `C:\Users\199X\OneDrive\Máy tính\olympic\dx-os`
- **Baseline Git Commit**: `1ecda7cdfa727a393b22654967e2ac7014ab2841`
- **Active Branch**: `main`
- **Remote**: `https://github.com/ROYCE-8425/open_source.git`
- **Beads Issue**: `open_source-cab.3` (Status: `in_progress`, claimed)
- **OpenSpec Change Tasks**: `BR001-R4.1` through `BR001-R4.4` remain unchecked `[ ]` awaiting independent Codex review.

### Exact Changed & Created Files
```
 M Directory.Packages.props
 M NuGet.Config
 M compose.yaml
 M Dockerfile
 M .env.example
 M scripts/check-contract.json
 M scripts/check.ps1
 M scripts/smoke-runtime.ps1
 M scripts/verify-check-contract.ps1
 M src/DXOS.Api/DXOS.Api.csproj
 M src/DXOS.Api/Program.cs
 M src/DXOS.Api/packages.lock.json
 M src/DXOS.AppHost/DXOS.AppHost.csproj
 M src/DXOS.AppHost/Program.cs
 M src/DXOS.AppHost/packages.lock.json
 M src/DXOS.Application/packages.lock.json
 M src/DXOS.Domain/packages.lock.json
 M src/DXOS.Infrastructure/DXOS.Infrastructure.csproj
 M src/DXOS.Infrastructure/packages.lock.json
 M src/DXOS.Infrastructure/Persistence/BootstrapDbContextFactory.cs
 M src/DXOS.Workflows/DXOS.Workflows.csproj
 M src/DXOS.Workflows/packages.lock.json
 M src/DXOS.Workflows/Smoke/EmitSmokeResultActivity.cs
 M src/DXOS.Workflows/Smoke/EngineeringSmokeWorkflow.cs
 M tests/DXOS.Architecture.Tests/packages.lock.json
 M tests/DXOS.Integration.Tests/packages.lock.json
 M tests/DXOS.Unit.Tests/packages.lock.json
?? .config/dotnet-tools.json
?? .dockerignore
?? artifacts/task-runs/open_source-cab.3/*
?? docs/runtime.md
?? src/DXOS.AppHost/Properties/launchSettings.json
?? src/DXOS.Infrastructure/Migrations/20260814104157_InitialBootstrap.Designer.cs
?? src/DXOS.Infrastructure/Migrations/20260814104157_InitialBootstrap.cs
?? src/DXOS.Infrastructure/Migrations/BootstrapDbContextModelSnapshot.cs
?? src/DXOS.Infrastructure/Persistence/BootstrapDbContext.cs
?? src/DXOS.Infrastructure/Persistence/Entities/RuntimeProbe.cs
```

---

## 3. Architecture and Dependency Boundaries

Layer dependency directions strictly adhere to OpenSpec specifications:

```
[ DXOS.AppHost ] --------> [ DXOS.Api ]
(Aspire.Hosting)               |
                               +----> [ DXOS.Workflows ] ------> [ Elsa 3.7.1 NuGet ]
                               |             |
                               |             v
                               +----> [ DXOS.Infrastructure ] -> [ EF Core 10.0.10 / Npgsql 10.0.3 ]
                               |             |
                               v             v
                       [ DXOS.Application ] ---> [ DXOS.Domain ]
```

- **`DXOS.Domain`**: Pure domain abstractions and entities. Zero external package references.
- **`DXOS.Application`**: Pure application interfaces and core probe requests. References only `DXOS.Domain`.
- **`DXOS.Infrastructure`**: Persistence (`BootstrapDbContext`), EF Core migrations, and runtime entities. References `DXOS.Domain`, `DXOS.Application`, `Microsoft.EntityFrameworkCore` 10.0.10, and `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3.
- **`DXOS.Workflows`**: Workflow definitions (`EngineeringSmokeWorkflow`) and custom activities (`EmitSmokeResultActivity`). References `DXOS.Application`, `Elsa` 3.7.1, `Elsa.Workflows.Core` 3.7.1, `Elsa.Workflows.Management` 3.7.1, and `Elsa.Workflows.Runtime` 3.7.1. Zero project reference to `DXOS.Infrastructure` or `DXOS.Api`.
- **`DXOS.Api`**: Host orchestration, Elsa workflow registration, EF Core DI registration, health probes (`/health/live`, `/health/ready`), and workflow execution endpoint (`POST /smoke/workflow`).
- **`DXOS.AppHost`**: Aspire orchestration host. Integrates PostgreSQL resource with deterministic run ID labeling (`dxos.run.id=<runId>`) and immutable image digest pinning.

---

## 4. Verification and Safety Evidence Summary

- **Single Frozen Run ID**: `33c7c91ea2e3`
- **Total Duration**: `295.55s`
- **Safety Contracts**: 11/11 PASSED (`safety-test-results.json`, `safety-transcript.log`)
- **29 Sequential Steps**: 29/29 PASSED (`verification-matrix.md`, `raw-logs/step-01-*.log` through `raw-logs/step-29-*.log`)
- **Lock File Immutability**: All 9 `packages.lock.json` files remained byte-identical before and after verification.
- **Pre-existing Docker Resources**: 4 containers, 8 networks, 25 volumes preserved with 0 state/config mutations.
- **Docker Residue**: 0 task-owned containers, networks, or volumes remaining.
- **Sidecar Checksums**: `verification-output.sha256` (57 entries) and `reports.sha256` (5 entries) generated and validated.
