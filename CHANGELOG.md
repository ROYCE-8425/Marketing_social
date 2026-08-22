# Changelog

All notable changes to the DX-OS project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Added
- Standardized open-source repository structure and documentation suite:
  - Canonical `Apache-2.0` license in `LICENSES/Apache-2.0.txt` and `REUSE.toml` specification.
  - Comprehensive `README.md`, `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `GOVERNANCE.md`, `SUPPORT.md`, `MAINTAINERS.md`, `ROADMAP.md`, `CITATION.cff`.
  - In-depth documentation under `docs/` (`getting-started.md`, `build-from-source.md`, `development.md`).
  - OpenSSF Scorecard, OSPS Baseline, and Best Practices Badge evaluations under `docs/oss/`.
  - Standardized GitHub community health files in `.github/` (issue forms, PR template, CODEOWNERS).
- Central Package Management (CPM) with locked mode across all 9 .NET projects.
- Elsa 3.7.1 workflow engine integration with custom marketing workflow execution.
- Automated quality gate engine with 26 foundation, runtime, and security checks (`scripts/check.ps1`).
- Multi-scanner security pipeline:
  - Gitleaks 8.30.0 secret detection.
  - Trivy 0.72.0 misconfiguration and vulnerability analysis.
  - Syft 1.50.0 CycloneDX 1.7 SBOM generation.
  - Grype 0.116.1 vulnerability scanning with zero HIGH/CRITICAL tolerance.
- Comprehensive test suite:
  - Unit tests with xUnit.net v3.
  - Architectural boundary rules with ArchUnitNET.
  - PostgreSQL database integration tests with Testcontainers.
- Orchestration support via Docker Compose and .NET Aspire 13.4.

### Changed
- Refactored project architecture to Clean Architecture Modular Monolith.
- Reconciled exact bi-directional open-source inventory (`artifacts/oss-inventory.json`) and SBOM (`artifacts/sbom.cdx.json`).
