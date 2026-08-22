# Security & Supply-Chain Rules

- Security verification is fail-closed and cannot be satisfied by LLM review alone.
- Deterministic scanner suite: Gitleaks (secret detection), Trivy (vulnerability scanning), Syft (SBOM generation), and Grype (vulnerability matching).
- All direct and transitive dependencies must be recorded in Directory.Packages.props with exact lock files preserved.
- Generate and validate deliverable CycloneDX SBOM at artifacts/sbom.cdx.json.
- Maintain complete dependency attribution in OPEN_SOURCE.md and THIRD_PARTY_NOTICES.md.