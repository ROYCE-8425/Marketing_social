# ADR-0001: DX-OS Open-Source License

- Status: Accepted
- Date: 2026-08-13
- Decision owners: DX-OS project owner and maintainers

## Context

DX-OS is intended to be an open-source software project with its own public-source identity. The project was extracted from an Elsa checkout, but DX-OS is neither Elsa nor an undisclosed Elsa fork. Reusing Elsa's LICENSE would not constitute a deliberate DX-OS licensing decision.

## Decision

DX-OS selects the Apache License, Version 2.0 (`Apache-2.0`) as its default project license. The canonical license text MUST be installed in the DX-OS-owned repository before public release. Package and release metadata MUST use the same SPDX expression.

A different OSI-compatible license may replace this decision only when verified competition, dependency, compatibility, or project requirements provide a stronger reason. That change requires a superseding ADR, compatibility review, notice/attribution review, and explicit owner approval.

This decision does not relicense third-party material. Each dependency or reused source remains under its upstream license, and all required notices and redistribution duties remain in force.

## Consequences

- DX-OS source is open source; the repository must become public before CLEAN CLONE / READY and competition release.
- `LICENSE`, `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, and `artifacts/sbom.cdx.json` must agree with the verified deliverable.
- Copied, adapted, vendored, or forked source must retain required notices and provenance.
- A temporary private repository or missing canonical license is an explicit release blocker, not evidence that DX-OS is proprietary.
- Legal or competition-specific review may still be required; this ADR records project policy and is not legal advice.

## Verification

- Confirm the repository license text is the canonical Apache-2.0 text.
- Confirm package/release metadata uses `Apache-2.0`.
- Reconcile direct/transitive dependencies, source provenance, notices, and SBOM.
- Reject public language that implies third-party services or all development tooling are open source.
