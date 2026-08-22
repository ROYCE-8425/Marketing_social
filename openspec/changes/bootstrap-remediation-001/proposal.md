## Why

The accepted [BOOTSTRAP AUDIT 001](../../../docs/audits/BOOTSTRAP-AUDIT-001.md) establishes a `NOT_READY` baseline: DX-OS is an untracked overlay on the Elsa Core repository, its quality gate can return success after real failures, its tests are placeholders, and its declared Elsa, Aspire, PostgreSQL, Docker, security, SBOM, OpenSpec, and CI capabilities are absent or non-functional. Remediation is required now because implementing product behavior on this foundation would make security, reproducibility, review evidence, and competition claims unreliable.

The business and competition impact is direct. A submission cannot credibly demonstrate a deployable end-to-end business process when a clean checkout cannot reproduce the build/runtime, a passing test suite contains no assertions, the source repository belongs to an upstream dependency, or required security and OSS evidence is missing.

## Verified Baseline

This change accepts the audit findings without reopening a broad audit:

- the current Git remote/history/build metadata belong to Elsa Core;
- `DXOS.slnx` does not compile or project-reference Elsa, but DX-OS inherits Elsa build/package configuration;
- DX-OS consumes no Elsa NuGet package today;
- `scripts/check.ps1` is false-green and does not fail on missing tools;
- `compose.yaml`, `global.json`, a real Aspire AppHost, application PostgreSQL connectivity, CI, SBOM, ADRs, project state, and `walkthrough.md` are absent;
- unit, integration, architecture, and E2E projects each contain an empty test;
- OpenSpec and Beads CLIs are callable through `.cmd`, but OpenSpec had no accepted change and the remediation graph has not yet been tied to an implementation contract;
- Context7, Docker daemon access, Gitleaks, Trivy, Syft, and Grype were not functional in the audited environment.

## What Changes

- **BREAKING**: extract reviewed DX-OS-owned artifacts into a new DX-OS-owned repository with independent history, remote, metadata, CI, and license posture while preserving the existing Elsa checkout as a reference/backup until clean-clone acceptance.
- Replace inherited Elsa build infrastructure with a minimal .NET 10 build using an SDK pin and a DX-OS-owned Central Package Management catalog.
- Define and enforce modular-monolith dependency direction without generic repositories, UnitOfWork wrappers, empty service pairs, microservices, or one-project-per-small-feature structure.
- Consume the minimum stable Elsa functionality through NuGet packages only and prove it with a smoke workflow; no Elsa source project may enter the DX-OS build graph.
- Add reproducible PostgreSQL, Aspire, and Docker Compose development/runtime paths with health and connectivity evidence.
- Replace placeholder tests with meaningful unit, architecture, and PostgreSQL integration tests; treat E2E as explicitly `NOT_APPLICABLE` until a real UI exists.
- Replace the false-green quality script with a Windows-safe, fail-fast command that verifies tools, propagates exit codes, and targets `DXOS.slnx` explicitly.
- Add deterministic CI, secret/vulnerability/container scanning, Syft SBOM generation, Grype or an approved equivalent, and accurate OSS/license disclosures.
- Establish DX-OS itself as an open-source project: an independently owned public source repository, an ADR-governed OSI-compatible license decision (Apache-2.0 by default), reproducible source-to-demo instructions, complete dependency/attribution/service disclosures, and provider-independent AI boundaries.
- Establish OpenSpec, Beads, agent-rule, task-evidence, and project-state procedures that are durable in the new repository.
- Require a clean-clone re-audit to prove every READY gate before any business feature specification starts.

## Non-Goals

- No business feature, CRM behavior, identity model, lead processing, campaign logic, UI product surface, or AI agent behavior.
- No deletion, history rewrite, force-push, or mutation of the existing Elsa checkout.
- No fork or modification of Elsa source.
- No microservice decomposition or speculative broker/cache infrastructure.
- No Kafka, Redis, RabbitMQ, or paid infrastructure without a separately accepted requirement.
- No generic `Repository<T>`, UnitOfWork wrapper over EF Core, empty `IService`/`Service` pairs, or project-per-small-feature layout.
- No E2E PASS claim based on an empty test; Playwright may be deferred until a real UI exists.
- No installation of tools merely to make research appear complete; installation belongs to reviewed implementation tasks.
- No product spec named `identity-organization-audit-foundation` until the clean-clone audit returns `READY`.

## Capabilities

### New Capabilities

- `repository-foundation`: DX-OS repository ownership, Git safety, .NET 10 pinning, minimal package/build configuration, solution boundaries, and clean extraction from the Elsa checkout.
- `engineering-runtime`: reproducible Elsa NuGet, PostgreSQL, Aspire, Docker Compose, health, and smoke-runtime behavior.
- `quality-evidence`: fail-fast local/CI gates, meaningful tests, architecture enforcement, security scanners, SBOM, OSS disclosures, and clean-clone verification evidence.
- `agent-governance`: OpenSpec contracts, Beads execution mapping, authority rules, task-run evidence, project state, and agent handoff requirements.

### Modified Capabilities

None. The main OpenSpec capability store is empty; this change introduces the Engineering OS contracts.

## Success Criteria

Bootstrap is `READY` only when a clean clone of the new DX-OS repository proves all of the following without access to the Elsa source checkout:

- repository identity, Git remote/history, DX-OS metadata, and agent instructions are correct;
- .NET 10 SDK selection, restore, formatting, and Release warning-clean build pass;
- solution/project dependencies obey executable modular-monolith rules;
- PostgreSQL integration and a meaningful real-database test pass;
- stable Elsa NuGet integration and an Elsa workflow smoke scenario pass;
- Aspire and Docker Compose start and expose verifiable health/readiness evidence;
- the quality gate fails immediately and non-zero when a required command or check fails;
- Gitleaks, Trivy, Syft SBOM, and Grype or an approved equivalent pass;
- DX-OS CI executes the same required gates;
- OpenSpec validates, Beads maps execution state, task evidence is durable, and project state is current;
- OSS/license documents describe actual resolved dependencies and retained third-party material;
- the DX-OS-owned source repository is public, carries its own approved OSS license and release metadata, and does not present itself as Elsa or an undisclosed fork;
- `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, `artifacts/sbom.cdx.json`, and the separate third-party-service disclosure reconcile the verified deliverable;
- clean-clone evidence proves clone, restore, build, infrastructure startup, DX-OS startup, and the documented demo without private source;
- no release or competition-ready claim is made while the project license, attribution, SBOM, service disclosure, clean-clone path, or public-source independence is incomplete;
- no empty test is counted as evidence and E2E is explicitly `NOT_APPLICABLE` unless a real UI exists;
- the follow-up bootstrap audit records `READY`.

## Impact

Affected systems include repository ownership and location, Git remotes/history, root build/package files, `DXOS.slnx`, project references, workflow/runtime composition, PostgreSQL and container configuration, Aspire AppHost, tests, scripts, CI, `.agents`, OpenSpec, Beads export/state transfer, task-run artifacts, and security/OSS documentation.

The extraction changes the authoritative repository but preserves the existing checkout until independent verification succeeds. No current business API compatibility is at risk because no business capability exists yet.
