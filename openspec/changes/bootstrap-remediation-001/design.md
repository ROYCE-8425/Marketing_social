## Context

See [proposal.md](proposal.md) for motivation and the accepted [BOOTSTRAP AUDIT 001](../../../docs/audits/BOOTSTRAP-AUDIT-001.md) for evidence. The current checkout is Elsa Core with untracked DX-OS files overlaid on top. `DXOS.slnx` lists only DX-OS-named projects, but those projects inherit Elsa-owned root build properties, package catalog, feeds, Git history, remote, agent instructions, and repository metadata. The visible DX-OS projects are mostly empty, the test projects are placeholders, and the quality script can return success after required failures.

The migration has two simultaneous constraints: preserve the current checkout as the only complete source of bootstrap artifacts, and prevent Elsa source/build inheritance from entering the new repository. The change therefore uses a copy-and-prove migration into a sibling repository rather than cleanup-in-place.

## Goals / Non-Goals

**Goals:**

- Establish an independently owned DX-OS repository that can be cloned, built, tested, scanned, and run without the Elsa source checkout.
- Make the supported .NET 10, package, project, runtime, test, security, evidence, and agent contracts executable.
- Preserve a small modular-monolith structure that supports vertical slices and applies Clean Architecture boundaries only where they protect domain or dependency direction.
- Give Aspire and Docker Compose distinct, testable roles around the same API, PostgreSQL, and Elsa smoke path.
- Preserve the old checkout and a reviewed inventory until a clean-clone re-audit proves the new repository READY.

**Non-Goals:**

- Product behavior, identity, organization, CRM, campaigns, AI agents, or a real UI.
- Modifying, forking, compiling, or vendoring Elsa source.
- A generic repository abstraction, UnitOfWork wrapper over EF Core, empty `IService`/`Service` pairs, or a project per small feature.
- Microservices or speculative Kafka, Redis, RabbitMQ, broker, cache, or distributed consistency infrastructure.
- Copying Elsa's NUKE build, multi-targeting, preview feeds, release automation, solution, package catalog, suppressions, or repository metadata.

## Decisions

### 1. Repository ownership and extraction boundary

The current checkout remains read-only reference/backup for implementation work. The extraction task creates a new sibling directory, defaulting to `../dx-os`, only after confirming that the resolved target is outside the current checkout and does not already contain user data. It creates a manifest containing source relative path, classification, action (`copy`, `recreate`, `exclude`), reason, byte size, and SHA-256 for every reviewed DX-OS artifact.

Only reviewed DX-OS-owned content is copied. Build outputs, Elsa source, Elsa solution/build files, upstream Git metadata, locks, caches, generated agent integrations, and inherited root configuration are excluded. Root `Directory.Build.props`, `Directory.Packages.props`, `NuGet.Config`, `global.json`, `.gitignore`, `LICENSE`, `AGENTS.md`, CI workflows, and package metadata are recreated from the accepted design rather than copied.

The new directory is initialized with its own Git history. It starts with no remote unless a Product Owner-approved DX-OS remote URL is already supplied; it must never reuse or push to an `elsa-workflows/elsa-core` remote. Remote creation and first push are an explicit owner-authorized step, not an implementer assumption.

Alternative rejected: mass-delete Elsa files from the existing checkout. That approach risks destroying the only copy, creates an unreviewable deletion diff, and retains upstream history/configuration mistakes.

### 2. Git history, Beads migration, and preservation

Elsa Git history is not rewritten, filtered, or imported. The extraction manifest and source audit give provenance for copied DX-OS text without importing upstream commits. The old checkout path, current revision, remotes, and inventory hash are recorded as migration evidence.

Beads state is migrated deliberately. Before extraction, export regular issues to a reviewed JSONL evidence file and take a Dolt-native backup. The new repository receives a newly initialized `dxos` Beads database and imports/restores only the reviewed project state using supported Beads commands; live lock files and an embedded database are not copied blindly. The task must prove that `open_source-cab` and all mapped children remain queryable before the old checkout is considered dispensable.

Alternative rejected: copying `.beads/embeddeddolt` while it is live. It can carry locks, workspace-specific metadata, or inconsistent state.

### 3. .NET 10 SDK and language policy

The new root pins SDK `10.0.302`, `rollForward: latestPatch`, and `allowPrerelease: false` in `global.json`. Projects target only `net10.0` during bootstrap. `LangVersion` follows the SDK-supported stable default rather than `latest`, preventing preview-language drift. Nullable and implicit usings remain enabled.

Alternative rejected: inheriting the installed `10.0.103` or omitting `global.json`; both make agent and CI behavior machine-dependent. Alternative rejected: multi-targeting net8/net9/net10; it is Elsa's package concern, not a DX-OS requirement.

### 4. Minimal Central Package Management

`Directory.Packages.props` contains only approved direct package versions used by DX-OS projects. `ManagePackageVersionsCentrally` is enabled; project files contain no ad hoc versions. Stable packages come from NuGet.org through a minimal `NuGet.Config` with `<clear />`; preview/private Elsa and unrelated upstream feeds are absent. Transitive pinning remains disabled unless a later security/compatibility decision records why it is required.

The initial approved catalog is derived from [research.md](research.md), including Elsa `3.7.1`, Aspire `13.4.6`, EF Core `10.0.10`, Npgsql EF provider `10.0.3`, Testcontainers PostgreSQL `4.13.0`, xUnit v3 `3.2.2`, and ArchUnitNET `0.13.3`. Packages are added only by the task that uses them and updates OSS evidence.

Alternative rejected: pruning Elsa's large catalog in place. Recreating a small catalog makes accidental dependencies and unsupported feeds visible.

### 5. Solution and dependency direction

`DXOS.slnx` contains only intentional DX-OS-owned production and test projects. The bootstrap keeps the current coarse project names only when they acquire a demonstrated responsibility; empty ceremony projects may be collapsed rather than preserved for symmetry.

The intended direction is:

```text
DXOS.Domain                 -> no DXOS project
DXOS.Application            -> DXOS.Domain
DXOS.Workflows              -> DXOS.Application, Elsa NuGet packages
DXOS.Infrastructure         -> DXOS.Application, DXOS.Domain, EF/Npgsql
DXOS.Api                    -> DXOS.Application, DXOS.Infrastructure, DXOS.Workflows
DXOS.AppHost                -> DXOS.Api plus Aspire hosting packages
tests                       -> only the production assemblies each suite verifies
```

Vertical slices live inside a module/project by capability. A slice may contain endpoint/command/handler/validation together. Domain-only logic stays framework-free; orchestration and persistence adapters stay at the edge. There is no mandatory layer-per-operation or interface-per-class rule.

Architecture tests enforce the graph and prohibit references to paths/projects under the Elsa checkout, generic repository/UnitOfWork types, and unintended infrastructure-to-domain inversions.

### 6. Elsa integration

The workflow boundary references the stable `Elsa` bundle package `3.7.1` through CPM. Official Elsa guidance identifies it as the primary bundle containing Core, Management, Runtime, Mediator, and API-common packages. One direct reference is smaller operationally than separately pinning the same essential graph and is appropriate for the first executable smoke.

The smoke workflow is a deterministic code-defined workflow with a known result. It must start, complete, and expose instance/status/correlation evidence. No Elsa source directory, project reference, preview feed, build target, or generated package from the old checkout is permitted. If later evidence shows the bundle brings unused surface with material cost, an ADR may replace it with the exact stable component packages after the smoke behavior is preserved.

Alternative rejected: referencing `src/modules/Elsa*` or copying Elsa build infrastructure. DX-OS is a consumer, not an Elsa fork.

### 7. PostgreSQL and data access

PostgreSQL 18.4 is the bootstrap server baseline. DX-OS uses EF Core 10.0.10 with `Npgsql.EntityFrameworkCore.PostgreSQL` 10.0.3. The database boundary lives in Infrastructure; application/domain code does not depend on EF-specific repository wrappers. `DbContext` is the unit-of-work implementation.

Readiness performs a real database operation and is distinct from liveness. Integration tests use `Testcontainers.PostgreSql` 4.13.0 to start an isolated real engine, apply migrations, perform a write/read or equivalent contract, and dispose it. No EF InMemory substitute counts as PostgreSQL evidence.

Credentials enter through environment/local secret facilities. Compose may declare variable references and development-only synthetic defaults where clearly documented, but real credentials never enter source, logs, prompts, or evidence.

### 8. Aspire and Docker Compose roles

Aspire `13.4.6` is the developer control plane. `DXOS.AppHost` becomes a real AppHost using `Aspire.Hosting.AppHost` and `Aspire.Hosting.PostgreSQL`, adds the API and a PostgreSQL database resource, wires references, waits for readiness, and exposes logs/traces/health.

`compose.yaml` is the reproducible local/demo deployment path. It builds or runs DX-OS-owned services plus PostgreSQL 18.4, declares health checks, named volumes, bounded startup dependencies, and no reference outside the repository. Aspire and Compose must each work without requiring the other at runtime.

Alternative rejected: using Compose validation as proof that Aspire starts, or vice versa. They protect different developer/demo paths.

### 9. Testing and E2E truthfulness

Test projects use xUnit v3 with Microsoft Testing Platform. The minimum counted foundation is:

- unit tests for real production behavior introduced by the runtime spike;
- ArchUnitNET tests for project/type rules and Elsa source decoupling;
- Testcontainers PostgreSQL integration tests for migration, connectivity, and persistence behavior;
- an Elsa workflow smoke that asserts terminal state and correlation evidence.

The empty E2E project and test are removed. E2E is recorded `NOT_APPLICABLE` until a real UI/end-to-end product surface exists; Playwright is not added during bootstrap.

### 10. Deterministic quality gate

`scripts/check.ps1` is the supported Windows entry point. It uses terminating error behavior, an explicit native-command wrapper, tool preflight, bounded timeouts, and immediate non-zero propagation. Every .NET command names `DXOS.slnx` or a specific project. Missing required tools fail; optional/N/A gates are encoded as policy with a reason and activation condition, never silently skipped.

The gate is built incrementally but its final contract covers restore, format verification, Release build with warnings as errors, unit/architecture/integration tests, Compose validation/startup, PostgreSQL readiness, Aspire startup, Elsa smoke, Gitleaks, Trivy, Syft, Grype, and OpenSpec validation. Machine-readable outputs go under a gitignored evidence/output directory and are copied into task/CI artifacts when required.

Each security tool receives a harmless canary test in its integration task so “installed but ineffective” cannot count as PASS.

### 11. Security scanners and supply-chain pinning

The approved baseline is Gitleaks CLI `8.30.0`, Trivy `0.72.0`, Syft `1.50.0`, and Grype `0.116.1`. Tool acquisition is documented and version/checksum verified; CI actions are pinned to full immutable commit SHAs. Mutable `latest` tags are prohibited.

Trivy receives stricter controls because of the March 2026 ecosystem compromise: never use affected `0.69.4`, Docker Hub `0.69.5`/`0.69.6`, or mutable action tags; verify release checksums/signatures or image digests. Gitleaks `8.30.1` is not selected because a current upstream issue reports a default-rule regression; the canary must prove the chosen version detects a synthetic token and returns non-zero.

Trivy scans repository/configuration and built container output; Syft generates CycloneDX JSON from the resolved deliverable; Grype scans that SBOM with an explicit severity/fix policy. Gitleaks scans the working tree and Git history.

### 12. CI semantics

DX-OS-owned CI runs on pull requests and the default branch. It restores using locked inputs, invokes the same contracts as local verification, separates result groups, uploads test/security/SBOM evidence, and fails when required tools or outputs are absent. CI configuration is recreated, not copied from Elsa.

Platform-specific wrappers are allowed, but Windows `scripts/check.ps1` remains executable and Linux CI steps must preserve the same target selection, thresholds, and exit semantics. All third-party actions are pinned by full SHA and annotated with their human-readable release.

### 13. OpenSpec, Beads, and authority

This change is the authoritative remediation contract. Beads remains the only execution graph. `tasks.md` maps one workstream contract to one Beads child while Beads stores status/dependencies. The graph is refined to eight workstream-sized issues; no Markdown checklist becomes a second status system.

Root `AGENTS.md` and `.agents/rules/**` are recreated as DX-OS-owned UTF-8 guidance. Authority is current user instruction, accepted OpenSpec, accepted ADRs, business rules, Beads acceptance, repository rules/skills, existing code patterns, then model assumptions. Gemini implements one task at a time; Codex independently reviews actual state and emits `PASS` or `FIX_REQUIRED`.

### 14. Task evidence and project state

Every issue writes durable evidence under `artifacts/task-runs/<task-id>/`. `prompt.md` precedes implementation. Reports list exact commands, exit codes, environment, changed files, known limitations, and artifact hashes where relevant. Raw chat is not evidence.

`docs/PROJECT_STATE.md` is created in the new repository and updated only after Codex PASS. It records milestone, accepted capabilities, current issue, blockers, decisions/debt, next issue, demo readiness, and risks.

### 15. OSS and license documentation

DX-OS is itself an open-source project, not merely a consumer of open-source dependencies. Its source lives in an independently owned public repository with its own identity, history, README, license, release metadata, CI, and reproducible build/run/demo instructions. The repository must not identify itself as Elsa, reuse Elsa's license as the DX-OS licensing decision, or conceal forked, copied, adapted, or vendored source.

ADR-0001 selects Apache License 2.0 as the DX-OS default because it is OSI-approved and includes an explicit patent grant. A different OSI-compatible license requires verified competition, dependency, compatibility, or project evidence and a superseding ADR. `OPEN_SOURCE.md` describes dependency policy and review. `THIRD_PARTY_NOTICES.md` records direct/runtime/tool dependencies, versions, licenses, source URLs, purposes, modification status, redistribution status, and attribution/NOTICE duties. `artifacts/sbom.cdx.json` is generated from the actual deliverable. Reused source preserves upstream notices and records the upstream project and version/tag/commit where practical plus material modifications.

External services are disclosed separately from OSS dependencies, including development AI tools and any runtime email/SMS, advertising, cloud, SaaS, or proprietary API. Proprietary development tooling does not change the DX-OS source license, but an undocumented proprietary paid runtime dependency is prohibited. Project language distinguishes DX-OS source, the primarily open-source runtime stack, disclosed OSS dependencies, disclosed third-party services, and development tooling; it must not claim "100% open source" without verified evidence.

ADR-0002 requires an AI provider boundary so domain logic is not permanently coupled to Gemini, OpenAI, or any other provider. The boundary must permit Gemini, an OpenAI-compatible provider, a local/open-weight model, and future providers where practical without rewriting business modules.

No Elsa source or documentation is redistributed. Elsa is named as an MIT NuGet dependency. Copied DX-OS-authored artifacts retain their own provenance record in the extraction manifest.

### 16. Clean-clone verification and READY transition

R8 clones the public DX-OS remote into an empty temporary directory on a supported machine/runner. It verifies repository identity and every READY gate without access to the old checkout, private source, or untracked files. The clean user journey is clone, locked dependency restore, source build, required infrastructure startup, DX-OS startup, and documented demo execution. The audit records PASS/FAIL/NOT_APPLICABLE separately with commands and exit codes. Any missing, skipped, placeholder, or environment-dependent required gate keeps the verdict below READY.

The competition release gate also requires the project OSS license, complete attribution, dependency inventory, CycloneDX SBOM, installation and Docker/Compose instructions, source-level documentation, release metadata, and third-party-service disclosure. Missing license, attribution, SBOM, service disclosure, clean-clone evidence, or independence from undisclosed private source is an unconditional release blocker.

Only after the follow-up report is READY may `identity-organization-audit-foundation` be proposed. It is not created by this change.

## Risks / Trade-offs

- **[Only complete bootstrap copy is the overlay]** → Hash and classify every candidate before copying; keep the old checkout unchanged until clean-clone READY.
- **[Sibling target path already contains data]** → Resolve absolute source/target paths and fail before writing unless the target is absent or an explicitly empty newly created directory.
- **[Approved remote is temporarily private and may be renamed]** → Preserve independent DX-OS history and the owner-approved remote; do not mark clean-clone READY until the repository is public, identifies DX-OS, and can be cloned without private-source access.
- **[Beads state can be lost or corrupted during extraction]** → Take JSONL export plus Dolt-native backup, avoid live DB copying, and compare issue/dependency queries after restore/import.
- **[SDK pin is newer than installed developer SDK]** → Fail explicitly with the required official SDK version; installation is a documented prerequisite, not an automatic hidden side effect.
- **[Elsa bundle adds transitive packages]** → Accept for the first supported smoke, inventory it in SBOM/notices, and permit later component minimization only through an ADR and equivalent tests.
- **[Aspire and Compose can drift]** → Test the same resource names, health contracts, connection settings, and smoke behavior in both paths.
- **[Security tool releases or databases drift]** → Pin tool binaries/actions and verify integrity; refresh vulnerability databases under explicit CI policy and record timestamps.
- **[Scanner false green]** → Run harmless canary fixtures that must produce the expected non-zero/detection before scanning real artifacts.
- **[Large initial scope]** → Keep workstreams dependency-ordered, task-scoped, and independently reviewable; no business feature is allowed to fill empty layers.

## Migration Plan

1. **Freeze and inventory:** record current revision/remotes, export and back up Beads, hash/classify DX-OS candidates, and create the new safe target.
2. **Extract:** copy only reviewed owned artifacts, recreate root identity/configuration, initialize independent Git/Beads, and prove no Elsa source/history/remote inheritance.
3. **Build foundation:** pin .NET 10, create minimal CPM/feed/solution metadata, establish project direction, and implement a fail-fast gate skeleton.
4. **Runtime and tests:** add stable Elsa NuGet, PostgreSQL, Aspire, Compose, health/smoke behavior, and meaningful unit/architecture/integration evidence.
5. **Governance and compliance:** install DX-OS rules, OpenSpec/Beads mappings, ADR/project state, CI, scanners, SBOM, and OSS disclosures.
6. **Independent verification:** configure an approved DX-OS remote, clone into an empty location, run the complete gate, and publish BOOTSTRAP AUDIT 002.
7. **Retire backup only by owner decision:** after READY, the old Elsa checkout may be archived or removed in a separate explicitly authorized action. This change never deletes it.

Rollback is always to stop using the new target and return to the untouched old checkout plus the extraction/Beads backups. No rollback step mutates Elsa history or deletes either repository.

## Open Questions

- The Product Owner approved `https://github.com/ROYCE-8425/open_source.git` as the current DX-OS remote and may rename it later. Its temporary private visibility is acceptable during remediation but blocks the public clean-clone/competition gate.
- On 2026-08-13 the Product Owner established DX-OS as an open-source project and approved Apache-2.0 as the default license decision, subject only to a verified superseding ADR. `LICENSE-PENDING` is therefore retired; the remaining work is attribution, SBOM, service disclosure, public visibility, and clean-clone verification.
