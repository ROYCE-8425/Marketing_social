# Open-Source License & Attribution Audit Report

**Task ID**: `FOSS-CONVERSION-001`  
**Execution Date**: 2026-08-19  
**Audit Scope**: Entire DX-OS codebase, direct/transitive NuGet packages, container images, security tools, and service disclosures.  
**Audit Status**: **100% COMPLIANT & RECONCILED**

---

## 1. Project Licensing Structure

- **Canonical Project License**: Apache License 2.0 (`LICENSE`, `LICENSES/Apache-2.0.txt`).
- **Copyright Attribution**: `NOTICE` states: `Copyright 2026 DX-OS Contributors`.
- **REUSE Specification**: `REUSE.toml` declares Apache-2.0 coverage across all source, test, script, and documentation files.

---

## 2. Direct Third-Party Open Source Components

All direct components are permissively licensed with full third-party notices maintained in [THIRD_PARTY_NOTICES.md](../../../THIRD_PARTY_NOTICES.md):

| Component | Version | Official Source | License | DX-OS Purpose | Modified | Redistributed |
| :--- | :--- | :--- | :--- | :--- | :---: | :---: |
| **Elsa** | 3.7.1 | https://github.com/elsa-workflows/elsa-core | MIT | Workflow engine core | No | Binary Package |
| **Microsoft.EntityFrameworkCore** | 10.0.10 | https://github.com/dotnet/efcore | MIT | ORM framework runtime | No | Binary Package |
| **Microsoft.EntityFrameworkCore.Design** | 10.0.10 | https://github.com/dotnet/efcore | MIT | EF Core migration tooling | No | Build Tool |
| **Microsoft.EntityFrameworkCore.Relational** | 10.0.10 | https://github.com/dotnet/efcore | MIT | Relational DB provider | No | Binary Package |
| **Npgsql.EntityFrameworkCore.PostgreSQL** | 10.0.3 | https://github.com/npgsql/efcore.pg | PostgreSQL License | PostgreSQL provider | No | Binary Package |
| **Aspire.Hosting.AppHost** | 13.4.6 | https://github.com/dotnet/aspire | MIT | Distributed app orchestration | No | Binary Package |
| **Aspire.Hosting.PostgreSQL** | 13.4.6 | https://github.com/dotnet/aspire | MIT | PostgreSQL resource host | No | Binary Package |
| **SSH.NET** | 2026.0.0 | https://github.com/sshnet/SSH.NET | MIT | Secure Shell client | No | Binary Package |
| **Microsoft.NET.Test.Sdk** | 17.13.0 | https://github.com/microsoft/vstest | MIT | Test runner platform | No | Test Dependency |
| **xunit.v3** | 3.2.2 | https://github.com/xunit/xunit | Apache-2.0 | Unit test framework | No | Test Dependency |
| **TngTech.ArchUnitNET** | 0.13.3 | https://github.com/TNG/ArchUnitNET | Apache-2.0 | Architecture assertions | No | Test Dependency |
| **TngTech.ArchUnitNET.xUnitV3** | 0.13.3 | https://github.com/TNG/ArchUnitNET | Apache-2.0 | Architecture xUnit integration | No | Test Dependency |
| **Testcontainers.PostgreSql** | 4.13.0 | https://github.com/testcontainers/testcontainers-dotnet | MIT | Database integration containers | No | Test Dependency |

---

## 3. Container Images & Security Tools

| Component | Version | License | Digest / Repository |
| :--- | :--- | :--- | :--- |
| **mcr.microsoft.com/dotnet/aspnet** | 10.0 | MIT | `sha256:207cc51496778557731c81ff670333d8ade4a4fec22768fd1be8e78474a84ecf` |
| **mcr.microsoft.com/dotnet/sdk** | 10.0 | MIT | `sha256:e1fc6e423f543119c406d24e2e687d67c569f18f04a37a8b0005d80ad0dcee80` |
| **postgres** | 18.4-alpine | PostgreSQL License | `sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15` |
| **Gitleaks** | 8.30.0 | MIT | `https://github.com/gitleaks/gitleaks` |
| **Trivy** | 0.72.0 | Apache-2.0 | `https://github.com/aquasecurity/trivy` |
| **Syft** | 1.50.0 | Apache-2.0 | `https://github.com/anchore/syft` |
| **Grype** | 0.116.1 | Apache-2.0 | `https://github.com/anchore/grype` |

---

## 4. Reconciled Package Statistics

- **Direct Packages**: 13
- **Transitive Packages**: 215
- **Total Packages Reconciled**: 228
- **Duplicate Packages**: 0
- **Missing / Extraneous Packages**: 0
- **Copyleft Incompatibilities (GPL / AGPL)**: 0
- **Private Machine Paths in SBOM / Notices**: 0
