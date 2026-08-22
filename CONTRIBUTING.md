# Contributing to DX-OS

Thank you for your interest in contributing to **DX-OS**! We welcome contributions, bug reports, documentation improvements, and feature proposals from everyone.

---

## Code of Conduct

All contributors are expected to adhere to our [Code of Conduct](CODE_OF_CONDUCT.md) (Contributor Covenant 2.1). Please read it before participating.

---

## Development Philosophy & Standards

1. **Independent Provenance**: DX-OS is an independent open-source project. All contributions must be original works licensed under Apache-2.0 or compatible permissive open-source licenses.
2. **No Paid Tool / Proprietary Agent Requirements**: The DX-OS development lifecycle and test suite must remain 100% executable on open-source, freely accessible developer tooling (.NET SDK, Docker, PowerShell). Contributors are never required to purchase or use proprietary AI agents or paid tools to contribute.
3. **Fail-Fast Quality Gates**: All code changes must compile with zero warnings in Release mode and pass the automated quality gate engine.
4. **Architecture Discipline**: Adhere strictly to the Modular Monolith architecture boundaries defined in `tests/DXOS.Architecture.Tests`. Domain models must remain independent of external frameworks.

---

## Getting Started

### Prerequisites
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Docker](https://www.docker.com/) & Docker Compose
- PowerShell 5.1+ or PowerShell Core (pwsh)

### Fork & Branch Workflow

1. Fork the official repository on GitHub: `https://github.com/ROYCE-8425/Marketing_social` (project name: DX-OS; GitHub repo: `Marketing_social`). The .NET OS was inherited from `ROYCE-8425/open_source`.
2. Clone your fork locally:
   ```bash
   git clone https://github.com/<your-username>/open_source.git dx-os
   cd dx-os
   ```
3. Create a feature branch:
   ```bash
   git checkout -b feature/my-new-feature
   ```

### Building and Testing

```bash
# 1. Restore locked dependencies
dotnet restore --locked-mode

# 2. Build the solution in Release mode
dotnet build -c Release --no-restore

# 3. Run the unit and architecture tests
dotnet test tests/DXOS.Unit.Tests
dotnet test tests/DXOS.Architecture.Tests

# 4. Run the full local quality gate
powershell -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Profile Full
```

---

## Submitting Pull Requests

1. Ensure all local tests and quality gates pass before opening a PR.
2. Follow our commit message conventions:
   - `feat: add email marketing trigger activity`
   - `fix: resolve EF Core concurrency issue in workflow runner`
   - `docs: update quick start guide for Aspire`
3. Provide a clear PR description explaining the motivation, changes made, and validation performed using our [Pull Request Template](.github/PULL_REQUEST_TEMPLATE.md).
4. Maintainers will review your PR, provide constructive feedback, and trigger CI checks.

---

## Proposing Architecture Changes (OpenSpec / ADR)

For significant architectural changes, additions of new dependencies, or changes to security boundaries:
1. Open an issue or discussion on GitHub to discuss the proposed direction.
2. Draft an Architectural Decision Record (ADR) under `docs/adr/` following the existing format.
3. Update `docs/adr/README.md` and consult with maintainers before implementation.

---

## Questions and Support

Have questions? Feel free to reach out via [SUPPORT.md](SUPPORT.md) or open a discussion once community forums are enabled on the public remote.
