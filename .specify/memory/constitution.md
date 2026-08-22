<!--
Sync Impact Report
- Version: initial -> 1.0.0
- Added principles: Open Source; Public Source/Reproducibility; Proportionate Architecture; OSS Transparency; Provider Independence
- Added sections: Mandatory OSS Artifacts; Development and Release Governance
- Templates synchronized: plan-template.md, spec-template.md, tasks-template.md
- OpenSpec synchronized: bootstrap-remediation-001 proposal, design, specs, and tasks
-->
# DX-OS Constitution

## Core Principles

### I. DX-OS Is Open Source (NON-NEGOTIABLE)
DX-OS itself MUST be an open-source software project. Team-developed source MUST be released from a DX-OS-owned public repository under a deliberately selected OSI-compatible license. Apache License 2.0 is the default; any different license requires verified competition, dependency, compatibility, or project evidence and a superseding ADR. Elsa's LICENSE is upstream evidence only and MUST NOT be reused as the DX-OS licensing decision.

This principle cannot be waived by a feature spec, implementation shortcut, private dependency, vendor agreement, agent instruction, or release deadline. A private repository MAY be used temporarily during bootstrap, but public-source ownership is mandatory before CLEAN CLONE / READY and competition release.

### II. Independent Public Source and Reproducibility
DX-OS MUST have its own identity, Git history, README, license, release metadata, CI, and reproducible instructions. It MUST NOT present itself as Elsa, another upstream project, or an undisclosed fork. A clean user with no access to private source MUST be able to clone, restore dependencies, build from source, start required infrastructure, start DX-OS, and execute the documented demo.

Copied, adapted, vendored, or forked OSS source MUST retain required copyright and license notices, name its upstream project and version/tag/commit where practical, describe material modifications, and meet redistribution obligations. Reuse MUST never be concealed.

### III. Modular Monolith with Proportionate Boundaries
DX-OS uses a modular monolith, vertical slices, and selective Clean Architecture where boundaries protect domain or dependency direction. Generic repository abstractions, UnitOfWork wrappers over EF Core, empty `IService`/`Service` pairs, one project per small feature, and premature microservices are prohibited unless an accepted requirement and ADR demonstrate concrete value.

Domain logic MUST remain independent of infrastructure and provider SDKs. Projects and abstractions MUST correspond to durable responsibilities, not textbook ceremony.

### IV. Verifiable OSS and Supply-Chain Transparency
Every directly used OSS component MUST be documented with component, version, source/project, license, DX-OS purpose, modification status, and redistribution status. The repository MUST maintain `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, and a deliverable-derived CycloneDX SBOM at `artifacts/sbom.cdx.json`. Resolved dependencies, tools, images, reused source, licenses, notices, and the SBOM MUST reconcile mechanically before release.

Security, build, test, scan, SBOM, and clean-clone claims require executable evidence. Missing tools or required artifacts MUST fail explicitly; a green build alone is insufficient. Project language MUST distinguish DX-OS source, the primarily open-source core runtime, OSS dependencies, third-party APIs/services, and development tooling. "100% open source" MUST NOT be claimed unless independently verified.

### V. Provider Independence and Service Disclosure
External services are not OSS dependencies and MUST be disclosed separately. Disclosure includes Gemini, OpenAI/Codex development tooling, email/SMS providers, advertising APIs, cloud services, SaaS, and proprietary APIs used by the demo or runtime. Proprietary development tooling does not make DX-OS proprietary, but an undocumented proprietary paid service MUST NOT be a mandatory runtime dependency.

AI integrations MUST use a DX-OS-owned abstraction so business modules are not permanently coupled to Gemini, OpenAI, or another provider. Where practical, adapters MUST permit Gemini, an OpenAI-compatible provider, a local/open-weight model, and future providers without rewriting domain logic.

## Mandatory OSS Artifacts

The repository MUST maintain and review:

- its canonical DX-OS OSS `LICENSE` and the ADR that selected it;
- `README.md` with source build, infrastructure, startup, and demo instructions;
- `OPEN_SOURCE.md` and `THIRD_PARTY_NOTICES.md` reconciled to actual dependencies and reused material;
- `docs/THIRD_PARTY_SERVICES.md` or an equivalent separate service disclosure;
- `artifacts/sbom.cdx.json` generated from the verified deliverable;
- CI and release metadata owned by DX-OS;
- provenance and modification records for copied, adapted, vendored, or forked source;
- CLEAN CLONE / READY audit evidence for the verified revision.

An incomplete artifact is a tracked blocker and MUST NOT be represented as complete.

## Development and Release Governance

Every specification, plan, task set, ADR, implementation review, CI workflow, and release review MUST include the applicable constitutional checks. The required authority order is: current user instruction, this constitution, accepted OpenSpec, accepted ADRs, business rules, Beads acceptance criteria, workspace rules, project skills, existing patterns, and model assumptions.

DX-OS MUST NOT be marked READY or competition-release-ready when its own OSS license is missing; attribution is incomplete; the SBOM is missing or stale; required third-party services are undisclosed; clean-clone restore/build/infrastructure/start/demo fails; the public repository lacks DX-OS identity, CI, release metadata, or reproducible instructions; or the application depends on undisclosed private source.

The final CLEAN CLONE / READY audit MUST report OSS license, dependency inventory, SBOM, notices/attribution, installation instructions, Docker/Compose deployment, source documentation, third-party-service disclosure, public repository identity, and source-to-demo reproducibility separately. No required gate may be silently skipped.

## Governance

This constitution supersedes lower-level project practices. Amendments require a documented rationale, an impact review of OpenSpec/ADRs/templates/CI/release gates, and a migration plan for affected work. Versioning follows semantic versioning: MAJOR for incompatible governance changes, MINOR for new principles or materially expanded obligations, and PATCH for clarifications that do not change meaning.

Reviewers MUST return `FIX_REQUIRED` when a change violates a MUST requirement. Beads and OpenSpec completion state MUST not advance until independent evidence satisfies the relevant gates.

**Version**: 1.0.0 | **Ratified**: 2026-08-13 | **Last Amended**: 2026-08-13
