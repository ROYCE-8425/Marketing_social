# Local Development Guide

This guide details the conventions, tools, workflows, and testing practices for developers contributing to DX-OS.

---

## 1. Solution Architecture

The solution structure follows Clean Architecture with strict directional dependencies:

```text
src/
  ├── DXOS.Domain/          # Pure business entities, value objects, domain logic (No external dependencies)
  ├── DXOS.Application/     # Application use cases, DTOs, interfaces, commands & queries
  ├── DXOS.Workflows/       # Elsa 3.7 workflow definitions, activities, orchestration graphs
  ├── DXOS.Infrastructure/  # EF Core 10, Npgsql database contexts, repositories, external adapters
  ├── DXOS.Api/             # ASP.NET Core API controllers, middleware, endpoints, Elsa server
  └── DXOS.AppHost/         # .NET Aspire orchestration host for distributed execution

tests/
  ├── DXOS.Unit.Tests/          # Unit tests (xUnit.net v3)
  ├── DXOS.Architecture.Tests/  # Architectural boundary enforcement (ArchUnitNET)
  └── DXOS.Integration.Tests/   # PostgreSQL integration tests (Testcontainers)
```

---

## 2. Coding Standards & Conventions

1. **C# Language Version**: C# 14 / `latest` with nullable reference types enabled (`<Nullable>enable</Nullable>`).
2. **Formatting**: Follow `.editorconfig` (4 spaces, file-scoped namespaces, braces on new lines).
3. **Compiler Warnings**: Warnings are treated as errors (`<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`). Code must compile cleanly.
4. **Architectural Rules**:
   - `DXOS.Domain` must not reference any other solution project or external framework.
   - `DXOS.Application` depends only on `DXOS.Domain`.
   - `DXOS.Workflows` and `DXOS.Infrastructure` implement interfaces defined in `DXOS.Application`.
   - `DXOS.Api` acts as the composition root.

---

## 3. Database Migrations (EF Core & PostgreSQL)

DX-OS uses EF Core with PostgreSQL (`Npgsql.EntityFrameworkCore.PostgreSQL`).

### Creating a New Migration

```bash
# Add a new migration from the solution root
dotnet ef migrations add <MigrationName> --project src/DXOS.Infrastructure --startup-project src/DXOS.Api --output-dir Persistence/Migrations
```

### Applying Migrations Locally

```bash
dotnet ef database update --project src/DXOS.Infrastructure --startup-project src/DXOS.Api
```

---

## 4. Running the Complete Quality Gate Engine

Before submitting a pull request, run the full quality gate script to verify code formatting, compilation, test suites, and multi-scanner security checks:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Profile Full
```

The script executes 26 distinct gates in fail-fast order:
1. `foundation-restore`: Verifies CPM lockfile integrity.
2. `foundation-format`: Verifies `.editorconfig` formatting.
3. `foundation-build`: Compiles all projects in Release mode with zero warnings.
4. `foundation-openspec`: Verifies OpenSpec change proposals and validations.
5. `foundation-hygiene`: Verifies absence of temporary or forbidden build artifacts.
6. `runtime-docker-compose` & `runtime-smoke-*`: Verifies container startup and API health.
7. `runtime-unit-tests`: Runs unit test suite.
8. `runtime-architecture-tests`: Asserts ArchUnitNET boundary rules.
9. `runtime-integration-tests`: Executes PostgreSQL integration tests.
10. `full-gitleaks-scan`: Detects any secrets or credentials across working tree and git log.
11. `full-trivy-scan`: Analyzes container files for misconfigurations.
12. `full-syft-sbom`: Generates standard CycloneDX SBOM.
13. `full-grype-scan`: Scans SBOM against vulnerability databases.
14. `full-security-summary`: Produces signed, atomic security summary.
15. `full-governance-*`: Verifies ADRs, agent rules, and project state.
16. `full-ci-parity`: Asserts GitHub Actions CI workflow matches local gate rules.
17. `full-oss-*`: Asserts exact license, attribution, inventory, and service disclosures.
