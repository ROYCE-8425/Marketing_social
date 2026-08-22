# Implementation Plan: [FEATURE]

**Branch**: `[branch]` | **Date**: [DATE] | **Spec**: [link]

## Summary

[Primary requirement and technical approach]

## Constitution Check

*GATE: Must pass before implementation and again before acceptance.*

- [ ] DX-OS remains an independently owned open-source project; no upstream identity or license is inherited.
- [ ] The change preserves the clean-clone source-to-demo path without private source.
- [ ] Architecture remains a modular monolith with vertical slices and no unjustified ceremony or microservices.
- [ ] New or changed OSS dependencies, reused source, licenses, notices, and SBOM impact are identified.
- [ ] Third-party services and proprietary APIs are disclosed separately from OSS dependencies.
- [ ] AI/provider-specific code remains behind a DX-OS-owned boundary where applicable.
- [ ] CI, release, and CLEAN CLONE / READY evidence impacts are defined and fail closed.

Any failed check MUST be resolved or documented as a release-blocking constitutional violation; it cannot be waived in this plan.

## Technical Context

**Language/Version**: [value]\
**Primary Dependencies**: [value]\
**Storage**: [value or N/A]\
**Testing**: [value]\
**Target Platform**: [value]\
**Constraints**: [value]

## Project Structure

[Concrete documentation, source, tests, infrastructure, and evidence paths]

## Verification and Release Evidence

[Exact local/CI commands, artifacts, disclosure updates, clean-clone impact, and release blockers]

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because | Approval/ADR |
|---|---|---|---|
| [none or item] | [reason] | [reason] | [link] |
