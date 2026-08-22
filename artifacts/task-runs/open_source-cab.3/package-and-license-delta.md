# BR001-R4 Package and License Delta Report

## Overview
This document records the dependency resolution, package licenses, consumption edges, and security boundaries introduced as part of **BR001-R4 Engineering Runtime Spike** in accordance with OpenSpec `bootstrap-remediation-001`.

Central Package Management (CPM) is strictly enforced via `Directory.Packages.props` with `<CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>`. All packages resolve exclusively from `nuget.org` (verified via `NuGet.Config`). No preview, floating, or project-local package versions exist in the solution.

---

## 1. Resolved Runtime Packages and License Metadata

| Package ID | Resolved Version | Source Feed | License / Expression | Purpose | Consuming Project(s) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `Elsa` | `3.7.1` | nuget.org | [MIT](https://licenses.nuget.org/MIT) | Workflow engine core execution, activity definitions, and runtime runner (`IWorkflowRunner`) | `src/DXOS.Workflows` |
| `Microsoft.EntityFrameworkCore` | `10.0.10` | nuget.org | [MIT](https://licenses.nuget.org/MIT) | Relational persistence abstraction, DbContext lifecycle, query pipeline | `src/DXOS.Infrastructure` |
| `Microsoft.EntityFrameworkCore.Design` | `10.0.10` | nuget.org | [MIT](https://licenses.nuget.org/MIT) | Design-time tooling for EF Core migrations (`PrivateAssets="all"`) | `src/DXOS.Infrastructure` (context), `src/DXOS.Api` (tooling startup host) |
| `Microsoft.EntityFrameworkCore.Relational` | `10.0.10` | nuget.org | [MIT](https://licenses.nuget.org/MIT) | Centrally pinned relational abstractions (transitive from Npgsql) | Central CPM Pinning |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.3` | nuget.org | [PostgreSQL License](https://licenses.nuget.org/PostgreSQL) | PostgreSQL driver and relational dialect provider for EF Core | `src/DXOS.Infrastructure` |
| `Aspire.Hosting.AppHost` | `13.4.6` | nuget.org | [MIT](https://licenses.nuget.org/MIT) | Developer orchestration runtime, dashboard, and service discovery | `src/DXOS.AppHost` |
| `Aspire.Hosting.PostgreSQL` | `13.4.6` | nuget.org | [MIT](https://licenses.nuget.org/MIT) | Containerized PostgreSQL resource lifecycle provider for Aspire AppHost | `src/DXOS.AppHost` |
| `xunit.v3` | `3.2.2` | nuget.org | [Apache-2.0](https://licenses.nuget.org/Apache-2.0) | Unit, integration, and architecture testing platform | `tests/*` |
| `Microsoft.NET.Test.Sdk` | `17.13.0` | nuget.org | [MIT](https://licenses.nuget.org/MIT) | MSBuild targets and props for test project compilation | `tests/*` |

---

## 2. Tooling and CLI Manifest Packages

| Tool Package ID | Pinned Version | Scope | Manifest Location | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| `dotnet-ef` | `10.0.10` | Repository Local | `.config/dotnet-tools.json` | Deterministic, repo-isolated EF Core migrations management (`dotnet tool run dotnet-ef`) |

---

## 3. Composition Edge Boundaries

The architectural layer boundaries and reference rules defined in OpenSpec `bootstrap-remediation-001` are preserved:

- **`DXOS.Domain`**:
  - `ProjectReference`: None
  - `PackageReference`: None
  - Boundary: Zero framework, ORM, workflow, or infrastructure dependencies.
- **`DXOS.Application`**:
  - `ProjectReference`: `DXOS.Domain`
  - `PackageReference`: None
  - Boundary: Application business contracts only.
- **`DXOS.Workflows`**:
  - `ProjectReference`: `DXOS.Application`, `DXOS.Domain`
  - `PackageReference`: `Elsa` (3.7.1)
  - Boundary: Elsa workflow definitions and activity implementations.
- **`DXOS.Infrastructure`**:
  - `ProjectReference`: `DXOS.Application`, `DXOS.Domain`
  - `PackageReference`: `Microsoft.EntityFrameworkCore`, `Microsoft.EntityFrameworkCore.Design`, `Npgsql.EntityFrameworkCore.PostgreSQL`
  - Boundary: PostgreSQL persistence, `BootstrapDbContext`, and migrations.
- **`DXOS.Api`**:
  - `ProjectReference`: `DXOS.Application`, `DXOS.Infrastructure`, `DXOS.Workflows`
  - `PackageReference`: `Microsoft.EntityFrameworkCore.Design` (PrivateAssets="all", required as the startup project for `dotnet-ef` CLI tooling)
  - Boundary: ASP.NET Core API host, `/health/*` endpoints, `/smoke/*` endpoints.
- **`DXOS.AppHost`**:
  - `ProjectReference`: `DXOS.Api`
  - `PackageReference`: `Aspire.Hosting.AppHost`, `Aspire.Hosting.PostgreSQL`
  - Boundary: Local developer orchestration and distributed tracing.

---

## 4. Package Lock Files and Immutability Matrix

All 9 project lock files are generated deterministically and enforced in locked mode (`dotnet restore DXOS.slnx --locked-mode`).

| Lock File Path | Size (Bytes) | SHA-256 Digest |
| :--- | :--- | :--- |
| `src\DXOS.Api\packages.lock.json` | 27843 | `fb0c2400e13173bacd896b00d2b5f2a776268fe081d4d408607e588d5086eee3` |
| `src\DXOS.AppHost\packages.lock.json` | 32997 | `e2f3bcd126448fa1e35c31e978849c02cb11a5f03940be4806737a48d1e1ac64` |
| `src\DXOS.Application\packages.lock.json` | 123 | `de040e22ff1ae053c4e99bdcff6d999717e8dd8914ecf8875437586e0764ffd5` |
| `src\DXOS.Domain\packages.lock.json` | 61 | `03eeadc5ef377c17f787ab65f41fb4c8a9c936bb7f7f4171111fdeec8a81cb46` |
| `src\DXOS.Infrastructure\packages.lock.json` | 13265 | `f10ea611c84d94edc4c312d4abb090e42bb0093dd9cdca46157c08187bb43dfe` |
| `src\DXOS.Workflows\packages.lock.json` | 29669 | `6b12e0ce20f583565a62cebdaaf38d83920a011bb13fd61aeb6e6179fb5646d6` |
| `tests\DXOS.Architecture.Tests\packages.lock.json` | 38839 | `40c414904d26ec8c8b6aa36c860da68dc81a8c4f750b2c4e9c9ad31c0c8766de` |
| `tests\DXOS.Integration.Tests\packages.lock.json` | 38839 | `40c414904d26ec8c8b6aa36c860da68dc81a8c4f750b2c4e9c9ad31c0c8766de` |
| `tests\DXOS.Unit.Tests\packages.lock.json` | 38839 | `40c414904d26ec8c8b6aa36c860da68dc81a8c4f750b2c4e9c9ad31c0c8766de` |

---

## 5. Non-Reconciliation Notice
This document reflects the runtime dependency delta for the R4 spike. Final third-party notices, license attributions, and SBOM generation remain scheduled for **BR001-R7** as specified by OpenSpec `bootstrap-remediation-001`.
