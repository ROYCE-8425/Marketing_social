# OpenSSF Scorecard Evaluation & Baseline

This document provides a comprehensive evaluation of DX-OS against the **OpenSSF Scorecard** security checks, detailing current implementations, evidence references, and improvement actions.

---

## Evaluation Summary

| OpenSSF Check | Score / Status | Current Implementation in DX-OS | Evidence / Reference |
| :--- | :---: | :--- | :--- |
| **Binary-Artifacts** | **10 / 10** | No compiled `.dll`, `.exe`, or binary blobs in Git repository. External security tools are fetched with verified SHA256 digests. | `scripts/security-tools.json`, `scripts/ensure-security-tools.ps1` |
| **Branch-Protection** | **Target 9+** | Branch protection rules defined on `main`: requires PR review, passing status checks, and blocks force pushes. | `docs/oss/GITHUB_SETTINGS.md` |
| **CI-Tests** | **10 / 10** | Automated GitHub Actions CI workflow triggers on every push and PR to `main`. Executes unit, architecture, and integration suites. | `.github/workflows/ci.yaml` |
| **CII-Best-Practices** | **In Progress** | OpenSSF Best Practices criteria mapped and documented in project. | `docs/oss/BEST_PRACTICES_BADGE.md` |
| **Code-Review** | **9 / 10** | Strict PR review requirements with GitHub `.github/CODEOWNERS` and PR templates. | `.github/CODEOWNERS`, `.github/PULL_REQUEST_TEMPLATE.md` |
| **Dangerous-Workflow** | **10 / 10** | Zero dangerous workflow patterns. No untrusted `pull_request_target` checkout triggers. | `.github/workflows/ci.yaml`, `scripts/verify-ci-parity.ps1` |
| **Dependency-Update-Tool** | **10 / 10** | Central Package Management (CPM) with deterministic lockfiles; Dependabot enabled. | `Directory.Packages.props`, `packages.lock.json` |
| **Fuzzing** | **N/A** | Domain parsing fuzz testing identified on medium-term roadmap. | `ROADMAP.md` |
| **License** | **10 / 10** | Canonical Apache-2.0 license file, REUSE.toml specification, and NOTICE file. | `LICENSE`, `LICENSES/Apache-2.0.txt`, `REUSE.toml`, `NOTICE` |
| **Maintained** | **10 / 10** | Active commit history, detailed roadmap, and issue tracking. | `CHANGELOG.md`, `ROADMAP.md` |
| **Packaging** | **8 / 10** | Multi-stage Docker container build with official Microsoft .NET 10 base images. | `Dockerfile`, `compose.yaml` |
| **Pinned-Dependencies** | **10 / 10** | GitHub Action steps pinned to immutable 40-character commit SHAs. CPM packages pinned with SHA256 content hashes. | `.github/workflows/ci.yaml`, `packages.lock.json` |
| **SAST** | **10 / 10** | Multi-scanner pipeline: Roslyn strict analysis (`TreatWarningsAsErrors`), Trivy misconfiguration analysis, and OpenSSF Scorecard action. | `scripts/check.ps1`, `.github/workflows/scorecard.yml` |
| **Security-Policy** | **10 / 10** | Comprehensive security policy with private vulnerability disclosure process and response timelines. | `SECURITY.md` |
| **Signed-Releases** | **Target** | Release tags signed via GPG during release cutting protocol. | `docs/RELEASE_CRITERIA.md` |
| **Token-Permissions** | **10 / 10** | All workflow jobs declare minimal, read-only permissions (`permissions: contents: read`). | `.github/workflows/ci.yaml`, `.github/workflows/scorecard.yml` |
| **Vulnerabilities** | **10 / 10** | Continuous SBOM vulnerability analysis via Syft and Grype with zero HIGH or CRITICAL findings tolerated. | `artifacts/security/security-summary.json` |

---

## OpenSSF Scorecard Automated Workflow

The automated OpenSSF Scorecard analysis is integrated into GitHub Actions via `.github/workflows/scorecard.yml`.
Results are published to GitHub Security Code Scanning and the OpenSSF REST API.
