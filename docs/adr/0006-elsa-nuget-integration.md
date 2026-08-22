# ADR-0006: Elsa Workflow Engine NuGet Integration

- Status: Accepted
- Date: 2026-08-14
- Decision owners: DX-OS project owner and maintainers

## Context

DX-OS uses Elsa as its core workflow orchestration engine. Previously, Elsa source projects were referenced directly, coupling DX-OS to Elsa's internal build system and preview packages.

## Decision

DX-OS consumes Elsa exclusively through stable, publicly available NuGet packages (Elsa 3.7.1) managed via Central Package Management (CPM) in Directory.Packages.props:

1. **Package Consumption**: All Elsa packages (Elsa, Elsa.Workflows.Core, Elsa.Workflows.Runtime, Elsa.Workflows.Management) are resolved from NuGet.org.
2. **No Source Coupling**: Zero ProjectReference or source code links to Elsa repository projects.
3. **Isolated Module**: Elsa workflow definitions and custom activities reside inside src/DXOS.Workflows.
4. **Deterministic Smoke Workflow**: A code-first workflow (EngineeringSmokeWorkflow) validates engine initialization, activity execution, and correlation state.

## Consequences

- Decouples DX-OS builds from Elsa repository source code.
- Predictable versioning and locked package restore via packages.lock.json.
- Upgrading Elsa versions follows standard NuGet package update procedures.

## Verification

- Inspect Directory.Packages.props to ensure Elsa packages are pinned to stable releases.
- Verify DXOS.slnx and all .csproj files contain zero Elsa ProjectReference entries.
- Run Elsa workflow integration smoke tests to verify runtime execution.