# Security Policy

DX-OS security policy and vulnerability disclosure guidelines.

## Supported Versions

During bootstrap remediation, only the `main` branch is supported for security fixes.

| Version / Branch | Supported |
|---|---|
| `main` | Yes |
| Pre-release / Spike | No |

## Reporting a Vulnerability

If you discover a security vulnerability in DX-OS:

1. **Do not create a public GitHub issue.**
2. Report the vulnerability privately via GitHub Security Advisories or by emailing the maintainers.
3. Provide a detailed summary including:
   - Description of the vulnerability;
   - Steps to reproduce or proof-of-concept;
   - Potential impact and affected components.

## Response Timelines

- **Initial Response**: Within 48 hours of report receipt.
- **Triage and Assessment**: Within 5 business days.
- **Fix and Disclosure**: Security patches will be prioritized and published with a release advisory.

## Security Controls and Automated Gates

DX-OS enforces automated security gates locally and in CI:

- **Secret Detection**: Gitleaks 8.30.0 runs against working tree and Git history with a zero-leak policy.
- **Configuration & Container Scans**: Trivy 0.72.0 scans Dockerfiles, Compose manifests, and deliverable container images for HIGH and CRITICAL misconfigurations and vulnerabilities.
- **SBOM Generation**: Syft 1.50.0 generates a CycloneDX JSON Software Bill of Materials (`artifacts/sbom.cdx.json`) from the deliverable.
- **Vulnerability Scanning**: Grype 0.116.1 scans the generated SBOM with policy enforcement (`--fail-on high`).
- **Dependency Pinning**: All package versions are managed centrally via Central Package Management (`Directory.Packages.props`) with locked mode restore.
