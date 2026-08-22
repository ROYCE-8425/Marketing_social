# Testing Rules

- Unit Tests: xUnit v3 with Microsoft.Testing.Platform runner (tests/DXOS.Unit.Tests).
- Architecture Tests: ArchUnitNET rules validating dependency directions and boundary rules (tests/DXOS.Architecture.Tests).
- Integration Tests: Testcontainers.PostgreSql with real migrations and health probe assertions (tests/DXOS.Integration.Tests).
- Never use in-memory EF providers or fake repositories when testing database persistence.
- Zero-test passes and empty test placeholders are strictly prohibited.
- Browser/UI E2E tests are explicitly documented as N/A during bootstrap remediation.
- All test fixtures must clean up task-owned Docker containers, networks, and volumes upon completion.