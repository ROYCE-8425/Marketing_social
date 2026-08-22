# DX-OS Testing Architecture & Quality Assurance Guide

## 1. Overview & Test Project Responsibilities

DX-OS employs a multi-tiered test strategy built on Microsoft.Testing.Platform (MTP) and xUnit v3 (`xunit.v3` 3.2.2) executing on .NET 10 (`net10.0`). The test runner is configured globally in `global.json` via the official .NET 10 schema:
```json
{
  "sdk": {
    "version": "10.0.302",
    "rollForward": "latestPatch",
    "allowPrerelease": false
  },
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

| Test Project | Location | Technology | Scope & Responsibility | External Dependencies |
| :--- | :--- | :--- | :--- | :--- |
| **DXOS.Unit.Tests** | `tests/DXOS.Unit.Tests/` | xUnit v3 (MTP) | DbContext factory resolution, precedence rules, EF metadata constraints | Zero (pure in-memory) |
| **DXOS.Architecture.Tests** | `tests/DXOS.Architecture.Tests/` | ArchUnitNET 0.13.3 + xUnit v3 | Clean Architecture layer dependency rules, anti-wrapper rules, project reference boundary validator | Zero (Roslyn / IL / XML metadata) |
| **DXOS.Integration.Tests** | `tests/DXOS.Integration.Tests/` | Testcontainers 4.13.0 + xUnit v3 | Real PostgreSQL container migrations, probe roundtripping, Elsa workflow lifecycle execution | Docker Engine (fail-closed) |
| **E2E Tests** | *(N/A)* | *(N/A)* | End-to-end browser / user journey tests | `NOT_APPLICABLE` (No UI) |

---

## 2. Unit Testing (`DXOS.Unit.Tests`)

### Scope & Guarantees
- **Zero External Dependencies**: Executes entirely in memory without requiring database engines, Docker, network sockets, or external configuration files.
- **Factory & Precedence Protection**: Tests `BootstrapDbContextFactory` fallback behaviors, explicit connection strings, and priority ordering (`DXOS_CONNECTION_STRING` -> `ConnectionStrings:DefaultConnection` -> `ConnectionStrings__DefaultConnection` -> `Database:ConnectionString`). Proves resolved connection strings (`Host=primary`, `Host=secondary`) and ensures fail-closed exceptions when no valid configuration is provided.
- **EF Core Model Constraints**: Validates entity metadata for `RuntimeProbe` (table name `runtime_probes`, primary key `Id`, required property invariants, and maximum column lengths).

### Local Execution Command
```powershell
dotnet test tests/DXOS.Unit.Tests/DXOS.Unit.Tests.csproj -c Release --no-build --no-restore -- --report-ctrf
```

---

## 3. Architecture Testing (`DXOS.Architecture.Tests`)

### Enforced Clean Architecture Rules
Using ArchUnitNET (`TngTech.ArchUnitNET`) and deterministic XML ProjectReference validation, 13 architectural rules are statically enforced:
1. **Domain Independence**: `DXOS.Domain` must not depend on `Application`, `Infrastructure`, `Workflows`, `Api`, or `AppHost`.
2. **Application Dependencies**: `DXOS.Application` may only depend on `Domain`, never on `Infrastructure`, `Workflows`, `Api`, or `AppHost`.
3. **Infrastructure Boundaries**: `DXOS.Infrastructure` may depend on `Domain` and `Application`, but never on `Api` or `AppHost`.
4. **Workflow Boundaries**: `DXOS.Workflows` may depend on `Domain` and `Application`, but never on `Api` or `AppHost`.
5. **No Generic Repository Wrappers**: Prohibits generic repository or generic Unit-of-Work wrappers (e.g. `IRepository<T>`, `GenericRepository`). DX-OS uses EF Core `DbContext` directly in application/infrastructure services.
6. **Domain Decoupling from EF Core / Elsa**: `DXOS.Domain` must never reference `Microsoft.EntityFrameworkCore` or Elsa namespaces.
7. **Production Project Reference Boundary**: Validates that all production `.csproj` files in `src/` contain zero `ProjectReference` entries pointing to `elsa-core` source checkouts or path-based dependencies (`ProjectFileBoundaryValidator`).
8. **Production Assemblies Isolation**: Production assemblies must never reference test assemblies.
9. **AppHost Isolation**: `DXOS.AppHost` is an orchestrator and must not be referenced by internal domain or application layers.
10. **Test Project Isolation**: Test projects must not depend on one another.
11. **Rule Sensitivity (Domain)**: Negative fixture (`ViolatingDomainFixture`) verifies that deliberate domain dependency violations fail the architecture rule.
12. **Rule Sensitivity (ProjectReference)**: Negative fixture (`ViolatingProjectReferenceFixture`) verifies that deliberate Elsa source checkouts fail the project boundary validator.

### Local Execution Command
```powershell
dotnet test tests/DXOS.Architecture.Tests/DXOS.Architecture.Tests.csproj -c Release --no-build --no-restore -- --report-ctrf
```

---

## 4. Integration Testing (`DXOS.Integration.Tests`)

### Testcontainers PostgreSQL Lifecycle
- **Pinned Image**: Uses the approved immutable PostgreSQL image:
  `postgres:18.4-alpine@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15`
- **Dynamic Port & Container Isolation**: Generates a cryptographically random container name (`dxos-test-pg-<guid>`) on a dynamically allocated host port with labels `dxos.task=open_source-cab.4` and `dxos.test.run=<runId>`.
- **Fail-Closed Docker Contract**: If the Docker daemon is unreachable or unavailable, the integration suite **fails closed immediately** with a descriptive connection error. It **never** silently skips or produces false positive passes.
- **Real EF Migrations**: Runs real Entity Framework Core database migrations (`Database.MigrateAsync()`) and verifies database readiness with `SELECT 1;`.
- **Entity Persistence & Roundtrip**: Inserts a `RuntimeProbe` record with UTC timestamps, reads it back across separate DbContext instances, and verifies schema roundtripping.
- **Elsa Workflow Execution**: Builds an isolated `IServiceCollection` with Elsa Workflows, registers `EngineeringSmokeWorkflow` and `EmitSmokeResultActivity`, runs the workflow to completion via `IWorkflowRunner`, and asserts:
  - Terminal sub-status `Finished`;
  - Terminal status `Finished`;
  - Output result payload `DXOS_SMOKE_OK`;
  - Exact correlation ID propagation.
- **Bounded Teardown**: Uses `IAsyncLifetime` with bounded `StopAsync(cts.Token)` (30s) followed by `DisposeAsync()` to ensure the PostgreSQL container is completely stopped and removed even if assertions fail.

### Local Execution Command
```powershell
dotnet test tests/DXOS.Integration.Tests/DXOS.Integration.Tests.csproj -c Release --no-build --no-restore -- --report-ctrf
```

---

## 5. End-to-End (E2E) Testing Policy

- **Status**: `NOT_APPLICABLE`
- **Rationale**: DX-OS currently consists of headless backend APIs, workflow engines, and distributed orchestration hosts without a real user interface or browser frontend.
- **Policy**: E2E tests are explicitly tracked in `scripts/check-contract.json` with status `NOT_APPLICABLE` and required `false`. They are not labeled as `PASS` or `SKIPPED`, and no heavy browser dependencies (Playwright, Selenium) are included.
- **Activation Trigger**: When a real user interface or frontend web application is added to DX-OS, an E2E gate will be activated.

---

## 6. Machine-Readable Reporting & CTRF Support

DX-OS test suites use the Common Test Report Format (CTRF) supported natively by the Microsoft.Testing.Platform runner via the `--report-ctrf` argument.

- **Report Filename**: Generated per run with unique naming (`ctrf-report-<guid>.json`).
- **Gate Validation**: The quality gate runner `scripts/run-test-project.ps1`:
  1. Validates that the test project is in the approved whitelist.
  2. Enforces strict destination path containment within approved evidence roots (`artifacts/quality-gate` or `artifacts/task-runs`).
  3. Executes the test suite with bounded timeout via native `dotnet test`.
  4. Verifies that the CTRF JSON report is produced.
  5. Parses the summary section (`results.summary`).
  6. Verifies `tests > 0` (zero-test executions fail closed).
  7. Verifies `failed == 0`.
  8. Verifies `skipped == 0` (unexpected test skips fail closed).
  9. Propagates native exit codes.

---

## 7. Running Quality Gates

### Running Foundation Profile
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/check.ps1 -Profile Foundation
```

### Running Runtime Profile (Includes All Test Suites & Smokes)
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/check.ps1 -Profile Runtime
```

### Contract Verification Suite
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/verify-check-contract.ps1
```

---

## 8. Process, Container, and Sandbox Safety Guarantees

1. **Path Safety**: All runner scripts validate repository boundaries via `Assert-SafePathChain`, rejecting path traversal (`..`), absolute path escapes, and reparse points/symlinks.
2. **Process Tree Cleanup**: Process timeouts use bounded execution windows and invoke recursive process tree termination (`taskkill.exe /T /F /PID <pid>`) to prevent orphaned processes.
3. **Container Residue Prevention**: Testcontainers uses automated Ryuk resource reap mechanisms and bounded `IAsyncLifetime` teardown to guarantee zero leftover Docker containers, networks, or volumes.
4. **Secret Sanitization**: All log outputs and JSON evidence redact sensitive secrets (e.g. `POSTGRES_PASSWORD`, `SECRET_KEY`).
