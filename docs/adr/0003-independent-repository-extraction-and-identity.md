# ADR-0003: Independent Repository Extraction and Identity

- Status: Accepted
- Date: 2026-08-13
- Decision owners: DX-OS project owner and maintainers

## Context

DX-OS was initially conceived alongside prototype workflows in an Elsa repository checkout. Continuing development inside or directly coupled to the Elsa repository created identity confusion, build fragility, and dependency entanglement. DX-OS requires its own repository, clean commit history, and distinct open-source identity.

## Decision

DX-OS is extracted into an independent git repository with its own root configuration, solution file (DXOS.slnx), CPM dependency catalog, and documentation.

1. **Clean Provenance**: DX-OS maintains an independent git history and does not inherit upstream git history or remote references.
2. **Distinct Identity**: DX-OS is not presented as Elsa or an Elsa fork. Upstream materials are properly attributed under THIRD_PARTY_NOTICES.md.
3. **Self-Contained Build**: The repository builds and tests entirely from source using standard .NET SDK and Docker commands without referencing external checkouts.

## Consequences

- The repository owns its build, test, CI, and release processes.
- All references to upstream Elsa solutions, build scripts (build.sh, build.ps1), NUKE targets, and source projects are eliminated.
- Clean-clone reproducibility is verified without access to private source or previous checkout paths.

## Verification

- Confirm git remote -v and git history reflect the DX-OS repository.
- Verify DXOS.slnx contains only DX-OS owned projects.
- Verify zero ProjectReference or file path links point to external Elsa source trees.