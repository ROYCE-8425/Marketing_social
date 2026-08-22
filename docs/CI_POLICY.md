# DX-OS CI Policy

Status: REQUIRED_BY_BR001-R7

DX-OS-owned CI must run on pull requests and the default branch and must preserve the failure semantics of the supported local Full gate. CI is evidence, not a substitute for the clean-clone audit.

## Required jobs or separately reported gates

- pinned .NET SDK selection, locked restore, format verification, Release build with warnings as errors, and meaningful tests;
- architecture, PostgreSQL, Elsa smoke, Aspire, and Docker/Compose validation as activated by bootstrap workstreams;
- Gitleaks, Trivy, Syft CycloneDX generation to `artifacts/sbom.cdx.json`, and Grype or an approved equivalent;
- strict OpenSpec validation and repository/agent-policy checks;
- DX-OS public-source identity, canonical license, README/build instructions, release metadata, and no Elsa/private-source project coupling;
- reconciliation of `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, reused-source provenance, the SBOM, and `docs/THIRD_PARTY_SERVICES.md`;
- a truthful-claims check that rejects unsupported "100% open source" language;
- retained, hashed evidence for the exact revision.

Required gates may not use `continue-on-error`, mutable action tags, silent tool skips, or weaker CI-only thresholds. A release/tag workflow must additionally fail on any blocker in `docs/RELEASE_CRITERIA.md`.

CI implementation remains a BR001-R7 task and is not complete merely because this policy exists.
