# Open Source Components

Status: RECONCILED

DX-OS source code is licensed under the Apache License 2.0 (see [LICENSE](LICENSE) and [ADR-0001](docs/adr/0001-dx-os-open-source-license.md)). External dependencies, tools, and runtime components retain their respective open-source licenses. Third-party services and AI development providers are disclosed separately in [docs/THIRD_PARTY_SERVICES.md](docs/THIRD_PARTY_SERVICES.md).

## Open Source Dependency Inventory

The complete normalized machine-readable inventory reconciling CPM definitions, locked transitive packages, container images, security tools, and service disclosures is maintained at [artifacts/oss-inventory.json](artifacts/oss-inventory.json).

### Direct Components Summary

| Component | Version | Source/Project | License | DX-OS Purpose | Modified | Redistributed |
|---|---|---|---|---|---|---|
| Microsoft.NET.Test.Sdk | 17.13.0 | https://github.com/microsoft/vstest | MIT | .NET test execution platform | No | Build / test output |
| xunit.v3 | 3.2.2 | https://github.com/xunit/xunit | Apache-2.0 | Unit and integration test foundation | No | Build / test output |
| Elsa | 3.7.1 | https://github.com/elsa-workflows/elsa-core | MIT | Workflow orchestration engine | No | Binary package |
| Microsoft.EntityFrameworkCore | 10.0.10 | https://github.com/dotnet/efcore | MIT | ORM core runtime | No | Binary package |
| Microsoft.EntityFrameworkCore.Design | 10.0.10 | https://github.com/dotnet/efcore | MIT | EF Core design-time tooling | No | Build / dev tool |
| Microsoft.EntityFrameworkCore.Relational | 10.0.10 | https://github.com/dotnet/efcore | MIT | Relational database abstractions | No | Binary package |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | https://github.com/npgsql/efcore.pg | PostgreSQL License | PostgreSQL EF Core provider | No | Binary package |
| Aspire.Hosting.AppHost | 13.4.6 | https://github.com/dotnet/aspire | MIT | .NET Aspire inner-loop orchestration | No | Binary package |
| Aspire.Hosting.PostgreSQL | 13.4.6 | https://github.com/dotnet/aspire | MIT | Aspire PostgreSQL resource host | No | Binary package |
| TngTech.ArchUnitNET | 0.13.3 | https://github.com/TNG/ArchUnitNET | Apache-2.0 | Architecture rule assertion engine | No | Test dependency |
| TngTech.ArchUnitNET.xUnitV3 | 0.13.3 | https://github.com/TNG/ArchUnitNET | Apache-2.0 | xUnit v3 integration for ArchUnitNET | No | Test dependency |
| Testcontainers.PostgreSql | 4.13.0 | https://github.com/testcontainers/testcontainers-dotnet | MIT | PostgreSQL integration test containers | No | Test dependency |
| SSH.NET | 2026.0.0 | https://github.com/sshnet/SSH.NET | MIT | Secure Shell client library | No | Binary package |
| postgres (Docker) | 18.4-alpine | https://www.postgresql.org/ | PostgreSQL License | Relational database runtime service | No | Container image |
| Gitleaks | 8.30.0 | https://github.com/gitleaks/gitleaks | MIT | Secret detection security scanner | No | Quality gate tool |
| Trivy | 0.72.0 | https://github.com/aquasecurity/trivy | Apache-2.0 | Vulnerability and misconfig scanner | No | Quality gate tool |
| Syft | 1.50.0 | https://github.com/anchore/syft | Apache-2.0 | CycloneDX SBOM generator | No | Quality gate tool |
| Grype | 0.116.1 | https://github.com/anchore/grype | Apache-2.0 | SBOM vulnerability scanner | No | Quality gate tool |

## Software Bill of Materials (SBOM)

The authoritative, machine-readable Software Bill of Materials is generated from the deliverable and committed at [artifacts/sbom.cdx.json](artifacts/sbom.cdx.json) in CycloneDX JSON format.

## Licensing Clarity

- **DX-OS Source Code**: Licensed under Apache-2.0.
- **AIECOS Social CRM foundation** (`chrome-extension/`, `sync-receiver/`, `admin-ui/`, `mcp-server/`, `examples/`, `docs/aiecos/`): MIT License, Copyright 2026 AIECOS. See [LICENSES/MIT-AIECOS.txt](LICENSES/MIT-AIECOS.txt) and [ADR-0008](docs/adr/0008-aiecos-foundation-dual-stack.md). Not relicensed.
- **Core Runtime Stack**: Fully open source (Elsa NuGet, PostgreSQL, ASP.NET Core, EF Core).
- **OSS Dependencies**: Explicitly listed above with their respective licenses.
- **Proprietary Services & APIs**: Disclosed separately in [docs/THIRD_PARTY_SERVICES.md](docs/THIRD_PARTY_SERVICES.md).
- **AI Development Tools**: Development tooling only; not part of the runtime or source distribution.
- **Truthful Language**: DX-OS does not claim "100% open source" without qualifying external services and development tools.
