# Open Source Project Security (OSPS) Baseline

This document defines the **Open Source Project Security (OSPS) Baseline** adherence for DX-OS across all core security categories.

---

## 1. Project & Repository Integrity

| Requirement | Implementation Status | Evidence / Location |
| :--- | :---: | :--- |
| **Open Source Licensing** | **COMPLIANT** | Apache-2.0 License in `LICENSE`, `LICENSES/Apache-2.0.txt`, `NOTICE`, and `REUSE.toml`. |
| **Clear Project Provenance** | **COMPLIANT** | Clean Git history with original commits, independent repository identity. |
| **Governance & Ownership** | **COMPLIANT** | Defined governance model, maintainer list, and decision processes in `GOVERNANCE.md` and `MAINTAINERS.md`. |
| **Contribution & Code of Conduct** | **COMPLIANT** | Explicit contribution guide in `CONTRIBUTING.md` and Contributor Covenant in `CODE_OF_CONDUCT.md`. |

---

## 2. Vulnerability Management & Coordinated Disclosure

| Requirement | Implementation Status | Evidence / Location |
| :--- | :---: | :--- |
| **Published Security Policy** | **COMPLIANT** | Explicit `SECURITY.md` covering supported versions and triage SLAs (48h initial response, 5d triage). |
| **Private Reporting Channel** | **COMPLIANT** | GitHub Private Vulnerability Reporting and confidential maintainer contact enabled. |
| **Automated Vulnerability Scanning** | **COMPLIANT** | Grype scans generated CycloneDX SBOM on every quality gate run with `--fail-on high`. |
| **Zero High/Critical Vulnerabilities** | **COMPLIANT** | `artifacts/security/security-summary.json` confirms 0 HIGH and 0 CRITICAL CVEs. |

---

## 3. Supply Chain & Dependency Management

| Requirement | Implementation Status | Evidence / Location |
| :--- | :---: | :--- |
| **Central Dependency Management** | **COMPLIANT** | CPM configured via `Directory.Packages.props`. |
| **Deterministic Dependency Locking** | **COMPLIANT** | `packages.lock.json` committed for all 9 projects with SHA256 integrity checks. |
| **Software Bill of Materials (SBOM)** | **COMPLIANT** | Syft generates standard CycloneDX 1.7 JSON SBOM committed at `artifacts/sbom.cdx.json`. |
| **Inventory Reconciliation** | **COMPLIANT** | Exact bi-directional match between CPM, lockfiles, images, security tools, and `artifacts/oss-inventory.json`. |
| **Attribution & Third-Party Notices** | **COMPLIANT** | `THIRD_PARTY_NOTICES.md` contains complete license texts for all direct dependencies. |

---

## 4. Build, Automation & CI Security

| Requirement | Implementation Status | Evidence / Location |
| :--- | :---: | :--- |
| **Action Step SHA Pinning** | **COMPLIANT** | All GitHub Action steps in `.github/workflows/` are pinned to immutable 40-character commit hashes. |
| **Least Privilege Tokens** | **COMPLIANT** | Workflow jobs explicitly restrict GITHUB_TOKEN to `permissions: contents: read`. |
| **Local / CI Parity** | **COMPLIANT** | `scripts/check.ps1` runs the identical 26 quality and security gates enforced in CI. |
| **Secret Detection** | **COMPLIANT** | Gitleaks 8.30.0 runs locally and in CI with zero tolerated leaks across tree and history. |
| **Compiler Hardening** | **COMPLIANT** | `.NET 10` with `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` and `<Nullable>enable</Nullable>`. |

---

## 5. Testing & Verification Assurance

| Requirement | Implementation Status | Evidence / Location |
| :--- | :---: | :--- |
| **Unit Testing** | **COMPLIANT** | xUnit.net v3 test suite covering domain entities, workflow validation, and commands. |
| **Architecture Boundary Testing** | **COMPLIANT** | 13 ArchUnitNET rules asserting Clean Architecture boundaries between Domain, Application, and Infrastructure. |
| **Integration Testing** | **COMPLIANT** | Testcontainers PostgreSQL integration tests validating database migrations and repositories. |
| **Preflight Fail-Closed Mechanics** | **COMPLIANT** | Gate execution engine halts on missing tools, invalid fixtures, or security policy violations. |
