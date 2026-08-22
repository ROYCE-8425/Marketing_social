# Tasks: [FEATURE NAME]

**Input**: Accepted specification and plan

## Format

`- [ ] T### [P?] Description with exact paths and dependencies`

## Phase 1: Governance and Provenance

- [ ] T001 Re-run the constitution check and record any ADR requirement.
- [ ] T002 Inventory dependency, license, reused-source, third-party-service, and SBOM impacts.
- [ ] T003 Define clean-clone, CI, release, and competition evidence before implementation.

## Phase 2: Tests and Implementation

- [ ] T004 Add failing tests or executable rules for changed behavior.
- [ ] T005 Implement the smallest conforming vertical slice without speculative abstractions.
- [ ] T006 Add or update provider adapters without leaking vendor SDK types into business modules.

## Phase 3: Transparency and Verification

- [ ] T007 Update `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, provenance records, and `docs/THIRD_PARTY_SERVICES.md` as applicable.
- [ ] T008 Generate and validate `artifacts/sbom.cdx.json` from the verified deliverable.
- [ ] T009 Run local and CI quality gates and preserve exact evidence.
- [ ] T010 Run the documented clean-clone source-to-demo path when release readiness is in scope.
- [ ] T011 Confirm all release blockers are clear; do not mark READY with missing license, attribution, SBOM, service disclosure, clean-clone evidence, or undisclosed private source.
