# Package and License Delta - BR001-R5

## 1. Newly Added Direct Package References

The following packages were added to `Directory.Packages.props` under Central Package Management (CPM):

| Package ID | Requested Version | Pinned Version | License | Used In | Purpose |
| :--- | :--- | :--- | :--- | :--- | :--- |
| **TngTech.ArchUnitNET** | `0.13.3` | `0.13.3` | Apache-2.0 | `tests/DXOS.Architecture.Tests` | Core ArchUnitNET engine for architecture rule evaluation |
| **TngTech.ArchUnitNET.xUnitV3** | `0.13.3` | `0.13.3` | Apache-2.0 | `tests/DXOS.Architecture.Tests` | xUnit v3 integration assertions for ArchUnitNET |
| **Testcontainers.PostgreSql** | `4.13.0` | `4.13.0` | Apache-2.0 | `tests/DXOS.Integration.Tests` | PostgreSQL container lifecycle management |
| **SSH.NET** | *(Transitive)* | `2026.0.0` | MIT | Transitive (Docker.DotNet / Testcontainers) | Security advisory resolution |

---

## 2. Preflight Advisory Resolution (SSH.NET)

- **Advisory ID**: `GHSA-q939-rpr3-3284` (Moderate severity)
- **Affected Component**: `SSH.NET < 2026.0.0`
- **Context**: `Testcontainers.PostgreSql 4.13.0` -> `Docker.DotNet 3.125.15` -> `SSH.NET 2016.1.0`.
- **Remediation**: Explicitly pinned `SSH.NET` version `2026.0.0` in `Directory.Packages.props`.
- **Validation**: `dotnet restore` with locked mode verified zero vulnerabilities and clean lock evaluation.

---

## 3. Retained Core Dependencies

| Package ID | Version | License | Target Framework |
| :--- | :--- | :--- | :--- |
| `xunit.v3` | `3.2.2` | Apache-2.0 | `net10.0` |
| `xunit.runner.visualstudio` | `3.1.5` | Apache-2.0 | `net10.0` |
| `Microsoft.Testing.Platform` | `1.5.3` | MIT | `net10.0` |
| `Microsoft.Testing.Extensions.CodeCoverage` | `17.14.2` | Non-commercial / MS EULA | `net10.0` |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | `10.0.0-preview.1` | PostgreSQL License | `net10.0` |
| `Elsa` | `3.4.4` | MIT | `net10.0` |
| `Elsa.Workflows.Core` | `3.4.4` | MIT | `net10.0` |
| `Elsa.Workflows.Runtime` | `3.4.4` | MIT | `net10.0` |

---

## 4. Lock File Inventory

All 9 projects in `DXOS.slnx` maintain strict `packages.lock.json` files:
1. `src/DXOS.Api/packages.lock.json`
2. `src/DXOS.AppHost/packages.lock.json`
3. `src/DXOS.Application/packages.lock.json`
4. `src/DXOS.Domain/packages.lock.json`
5. `src/DXOS.Infrastructure/packages.lock.json`
6. `src/DXOS.Workflows/packages.lock.json`
7. `tests/DXOS.Architecture.Tests/packages.lock.json`
8. `tests/DXOS.Integration.Tests/packages.lock.json`
9. `tests/DXOS.Unit.Tests/packages.lock.json`
