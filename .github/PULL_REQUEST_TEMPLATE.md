## Description

Provide a clear and concise summary of the changes introduced in this pull request and the motivation behind them.

Fixes #(issue) / Related to #(issue)

## Type of Change

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] Breaking change (fix or feature that would cause existing functionality to not work as expected)
- [ ] Documentation update
- [ ] Refactoring / Performance improvement
- [ ] CI / Build tooling update

## Architectural & Governance Verification

- [ ] Follows Modular Monolith clean architecture boundaries (`DXOS.Domain` is independent).
- [ ] No new third-party dependencies added without prior discussion / ADR update.
- [ ] All new packages managed via `Directory.Packages.props` with updated `packages.lock.json`.
- [ ] No secrets, credentials, or private machine paths included.

## Quality Gate Checklist

- [ ] Solution compiles in Release mode with zero warnings (`dotnet build -c Release`).
- [ ] Unit tests pass (`dotnet test tests/DXOS.Unit.Tests`).
- [ ] Architectural tests pass (`dotnet test tests/DXOS.Architecture.Tests`).
- [ ] Integration tests pass (`dotnet test tests/DXOS.Integration.Tests`).
- [ ] Local quality gate passes cleanly (`powershell -File scripts/check.ps1 -Profile Full`).
