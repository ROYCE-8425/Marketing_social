# Bootstrap Remediation Tasks

OpenSpec defines the implementation contract below. Beads epic `open_source-cab` is the sole execution-state graph; checkbox state is updated only after Codex independently issues PASS for the mapped scope. All paths refer to the new DX-OS repository after BR001-R1 unless explicitly labeled old checkout.

## BR001-R1 Repository Extraction

**Beads mapping:** `open_source-cab.1` (all BR001-R1.x tasks). **Dependencies:** none.

- [ ] 1.1 **BR001-R1.1 Snapshot source and inventory candidates.**
  - **Objective:** Capture immutable provenance and classify every candidate DX-OS artifact before any copy.
  - **Dependencies:** Accepted audit/change only.
  - **Allowed scope:** Read old checkout; write `artifacts/task-runs/open_source-cab.1/inventory.csv`, `extraction-manifest.json`, and Beads export/backup evidence.
  - **Forbidden changes:** No deletion, move, Git history/remote edit, build cleanup, secret export, or broad copy.
  - **Acceptance criteria:** Revision/remotes/status, size/SHA-256, owner/classification/action/reason are recorded; `bin`, `obj`, locks, caches, Elsa source/build, inherited root config are excluded; Beads regular-issue JSONL and Dolt backup status exist.
  - **Verification commands:** `git rev-parse HEAD`; `git remote -v`; `git status --short`; `bd.cmd export -o <evidence>\issues.jsonl`; `bd.cmd backup status`; manifest schema/hash validation command documented by implementer.
  - **Required evidence:** Inventory, manifest, Git snapshot, Beads export/backup status, assumptions and exclusions in `verification.md`.
  - **Rollback notes:** Stop without filesystem cleanup; old checkout remains the recovery source.

- [ ] 1.2 **BR001-R1.2 Create safe target and copy reviewed owned artifacts.**
  - **Objective:** Materialize the approved manifest in a new empty sibling repository directory.
  - **Dependencies:** BR001-R1.1.
  - **Allowed scope:** Create default `../dx-os` only after absolute-path/emptiness checks; copy manifest entries marked `copy`; create explicit placeholders for entries marked `recreate`.
  - **Forbidden changes:** No write inside Elsa source paths, no mass delete, no overwrite of an existing target, no `.git` copy, no inherited build/package/license/CI root-file copy.
  - **Acceptance criteria:** Target resolves outside old checkout, was empty, copied hashes match, excluded paths are absent, and every target file resolves to a manifest action.
  - **Verification commands:** `Resolve-Path <old>,<new>`; `Get-ChildItem -Force <new>`; hash comparison; `rg --files <new> | rg '(^|[\\/])(bin|obj|src[\\/]modules[\\/]Elsa)([\\/]|$)'`.
  - **Required evidence:** Resolved paths, pre-write target check, copy log, post-copy hashes, exclusion query output.
  - **Rollback notes:** Leave incomplete target intact for review; rename/quarantine only with user approval and retry into another empty path.

- [ ] 1.3 **BR001-R1.3 Establish independent Git and Beads identity.**
  - **Objective:** Initialize new DX-OS Git/Beads state without importing Elsa history or remote.
  - **Dependencies:** BR001-R1.2.
  - **Allowed scope:** `git init`, DX-OS default branch/initial history, supported `bd init` plus reviewed restore/import, new-repository metadata/evidence.
  - **Forbidden changes:** No Elsa remote, force push, upstream push, history filter, direct live embedded-Dolt copy, fabricated remote URL, or public push.
  - **Acceptance criteria:** Git root/history is new; remote is approved DX-OS URL or explicitly absent as `REMOTE_PENDING_OWNER_AUTHORIZATION`; `open_source-cab` and eight mapped children/dependencies/spec IDs are queryable; no cycles.
  - **Verification commands:** `git -C <new> log --oneline --decorate -1`; `git -C <new> remote -v`; `bd.cmd -C <new> show open_source-cab --json`; `bd.cmd -C <new> dep cycles`.
  - **Required evidence:** Git identity/remote result, Beads restoration method and comparison, issue/dependency outputs.
  - **Rollback notes:** Recover Beads from the saved export/native backup; never repair by copying live lock/database files.

- [ ] 1.4 **BR001-R1.4 Prove extraction and publish implementation report.**
  - **Objective:** Give Codex a bounded, independently reviewable extraction result.
  - **Dependencies:** BR001-R1.1–R1.3.
  - **Allowed scope:** Read both repositories; write R1 `implementation-report.md` and `verification.md` in both evidence locations.
  - **Forbidden changes:** No build/runtime implementation, no deletion of old checkout, no claim that pending remote/license gates passed.
  - **Acceptance criteria:** Old checkout is preserved; new tree contains only reviewed DX-OS content; solution/projects have no Elsa source reference; known remote/license blockers are explicit.
  - **Verification commands:** `git -C <old> status --short`; `git -C <new> status --short`; `dotnet sln <new>\DXOS.slnx list`; `rg -n --glob '*.csproj' 'ProjectReference|Elsa' <new>`; full manifest reconciliation.
  - **Required evidence:** Changed/created files, exact commands/exit codes, unresolved external steps, manifest hash, no-merge/no-push declaration.
  - **Rollback notes:** Use untouched old checkout and evidence backups; do not remove either copy.

## BR001-R2 Minimal DX-OS Build System

**Beads mapping:** `open_source-cab.2` (all BR001-R2.x tasks). **Dependencies:** BR001-R1 PASS.

- [x] 2.1 **BR001-R2.1 Recreate SDK and root build policy.**
  - **Objective:** Make .NET 10 compilation policy DX-OS-owned and deterministic.
  - **Dependencies:** BR001-R1.
  - **Allowed scope:** `global.json`, `Directory.Build.props/targets`, `.editorconfig`, `.gitignore`, DX-OS repository/package metadata.
  - **Forbidden changes:** No Elsa URLs/authorship/icons/suppressions, preview language, multi-targeting, blanket warning suppression, or automatic SDK installation.
  - **Acceptance criteria:** SDK 10.0.302/latestPatch/no-prerelease and net10-only policy are explicit; Release warnings are errors; generated outputs are ignored.
  - **Verification commands:** `dotnet --version`; `dotnet msbuild DXOS.slnx -getProperty:TargetFramework -getProperty:TreatWarningsAsErrors`; `rg -n 'Elsa|LangVersion>latest|NoWarn' Directory.Build.* global.json`.
  - **Required evidence:** Effective SDK/MSBuild properties and old-vs-new root metadata summary.
  - **Rollback notes:** Revert only recreated DX-OS root files to the R1 snapshot.

- [x] 2.2 **BR001-R2.2 Recreate minimal CPM and NuGet feeds.**
  - **Objective:** Admit only current approved direct dependencies from stable sources.
  - **Dependencies:** BR001-R2.1.
  - **Allowed scope:** `Directory.Packages.props`, `NuGet.Config`, lock-file policy, package/license evidence.
  - **Forbidden changes:** No preview/private Elsa feeds, copied upstream catalog, floating versions, ad hoc project versions, or unused packages.
  - **Acceptance criteria:** CPM enabled; NuGet.org is the only required source; catalog contains only packages referenced by retained projects; versions match research or a documented OpenSpec update.
  - **Verification commands:** `dotnet nuget list source`; `dotnet restore DXOS.slnx`; `dotnet list DXOS.slnx package --include-transitive`; `rg -n 'feedz|Version=' NuGet.Config src tests`.
  - **Required evidence:** Direct/transitive package list, feed list, version/license reconciliation.
  - **Rollback notes:** Revert CPM/feed changes; never restore Elsa's catalog as fallback.

- [x] 2.3 **BR001-R2.3 Establish solution and project direction.**
  - **Objective:** Retain the smallest coarse DX-OS project graph that enforces the approved modular-monolith boundary.
  - **Dependencies:** BR001-R2.1–R2.2.
  - **Allowed scope:** `DXOS.slnx`, DXOS project files, minimal project-reference validation, removal/collapse of empty ceremony projects when justified.
  - **Forbidden changes:** No Elsa project/source reference, circular reference, business code, microservice, broker/cache, generic repository/UnitOfWork/service-pair scaffold, or project per small feature.
  - **Acceptance criteria:** Solution contains only intentional DX-OS projects; references match design direction; each retained project has a stated bootstrap responsibility.
  - **Verification commands:** `dotnet sln DXOS.slnx list`; `dotnet list DXOS.slnx reference`; architecture-graph validation command; `rg -n --glob '*.csproj' 'Elsa|\.\.[\\/].*open_source' .`.
  - **Required evidence:** Project inventory/responsibility, dependency graph, cycle/source-coupling result.
  - **Rollback notes:** Restore last passing new-repository project graph; do not recover from Elsa solution files.

- [x] 2.4 **BR001-R2.4 Prove clean restore and Release build.**
  - **Objective:** Demonstrate the recreated foundation independently of old outputs/caches.
  - **Dependencies:** BR001-R2.1–R2.3.
  - **Allowed scope:** Clean generated outputs, restore/build evidence, lock files if policy requires them.
  - **Forbidden changes:** No warning suppression to force green, no build against `Elsa.sln`, no copying old `bin/obj`, no unreviewed dependency upgrade.
  - **Acceptance criteria:** Explicit `DXOS.slnx` restore and Release build with warnings-as-errors pass from clean outputs; no generated artifact is tracked.
  - **Verification commands:** remove only validated new-repo `bin/obj`; `dotnet restore DXOS.slnx --locked-mode`; `dotnet build DXOS.slnx -c Release --no-restore -warnaserror`; `git status --short`.
  - **Required evidence:** Commands, exit codes, SDK, warnings, resolved packages, clean status/artifact check.
  - **Rollback notes:** Delete only new-repository generated outputs after absolute-path validation; source rollback remains Git revert.

## BR001-R3 Deterministic Quality Gate

**Beads mapping:** `open_source-cab.7` (all BR001-R3.x tasks). **Dependencies:** BR001-R2 PASS.

- [x] 3.1 **BR001-R3.1 Define gate profiles and evidence contract.**
  - **Objective:** Specify `Foundation`, `Runtime`, and default `Full` checks without creating bypass semantics.
  - **Dependencies:** BR001-R2.
  - **Allowed scope:** Gate script interface, required-check manifest, evidence schema, development documentation.
  - **Forbidden changes:** No silent optional checks, generic auto-discovery, E2E PASS, or success for not-yet-implemented Full checks.
  - **Acceptance criteria:** Each profile has explicit required tools/inputs/commands/outputs/timeouts; Full contains every READY gate; E2E alone is N/A with reason/activation.
  - **Verification commands:** script syntax parse; documented manifest inspection; invoke `scripts/check.ps1 -Profile Full` and confirm first absent downstream requirement is non-zero.
  - **Required evidence:** Interface/manifest table, sample failure output, profile-difference rationale.
  - **Rollback notes:** Revert R3 documentation/script skeleton only; retain audit evidence of the false-green predecessor.

- [x] 3.2 **BR001-R3.2 Implement native fail-fast runner and preflight.**
  - **Objective:** Ensure missing tools and native command failures terminate with the original failure visible.
  - **Dependencies:** BR001-R3.1.
  - **Allowed scope:** `scripts/check.ps1` and narrowly scoped helpers/output ignore rules.
  - **Forbidden changes:** No `$LASTEXITCODE` loss, `Continue` error policy, catch-and-success, unbounded process, secrets in logs, or ambiguous solution selection.
  - **Acceptance criteria:** Preflight occurs before dependent checks; native exit/duration/output are captured; first failure stops; final process is non-zero; all .NET operations name DXOS targets.
  - **Verification commands:** `powershell.exe ... scripts\check.ps1 -Profile Foundation`; controlled missing-tool and failing-native-command invocations from contract verifier.
  - **Required evidence:** Success and negative outputs with exit codes, sanitized command transcript.
  - **Rollback notes:** Revert helper/gate changes together so partial error handling cannot remain active.

- [x] 3.3 **BR001-R3.3 Add gate contract verifier and Foundation wiring.**
  - **Objective:** Make fail-fast behavior regression-testable and run real foundation gates.
  - **Dependencies:** BR001-R3.1–R3.2.
  - **Allowed scope:** `scripts/verify-check-contract.ps1`, temporary test fixtures under gitignored output, Foundation restore/format/build/OpenSpec steps.
  - **Forbidden changes:** No production file mutation by verifier, no real secret fixture, no assumption that Full passes before R4/R5/R7.
  - **Acceptance criteria:** Verifier proves missing tool, native failure, later-success masking, timeout, and unrelated-solution cases; Foundation passes; Full explicitly fails on unimplemented requirements.
  - **Verification commands:** `powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\verify-check-contract.ps1`; same invocation for `check.ps1 -Profile Foundation` and `-Profile Full`.
  - **Required evidence:** Contract case matrix and exit codes, Foundation result, expected Full failure reason.
  - **Rollback notes:** Remove only validated temporary fixtures; revert verifier/gate wiring as one unit.

## BR001-R4 Engineering Runtime Spike

**Beads mapping:** `open_source-cab.3` (all BR001-R4.x tasks). **Dependencies:** BR001-R2 PASS; may run in parallel with R3.

- [x] 4.1 **BR001-R4.1 Add approved runtime dependencies and composition boundaries.**
  - **Objective:** Introduce stable Elsa/EF/Npgsql/Aspire packages only at their intended edges.
  - **Dependencies:** BR001-R2.
  - **Allowed scope:** CPM, `DXOS.Workflows`, `DXOS.Infrastructure`, `DXOS.Api`, `DXOS.AppHost` project references/composition.
  - **Forbidden changes:** No Elsa source/project reference or preview feed, package version in project files, speculative integration, or product endpoint.
  - **Acceptance criteria:** Elsa 3.7.1, EF 10.0.10, Npgsql provider 10.0.3, Aspire 13.4.6 resolve through CPM and follow design direction.
  - **Verification commands:** `dotnet restore DXOS.slnx --locked-mode`; `dotnet list DXOS.slnx package --include-transitive`; `rg -n --glob '*.csproj' 'Elsa|ProjectReference|Version=' src`.
  - **Required evidence:** Resolved dependency graph and license delta.
  - **Rollback notes:** Revert package/project changes; never replace failed packages with old source references.

- [x] 4.2 **BR001-R4.2 Implement PostgreSQL bootstrap persistence and health.**
  - **Objective:** Prove migration-capable PostgreSQL access and real readiness without abstraction ceremony.
  - **Dependencies:** BR001-R4.1.
  - **Allowed scope:** Infrastructure `DbContext`, minimal bootstrap entity/migration needed only for engineering proof, API liveness/readiness composition and secret-safe config.
  - **Forbidden changes:** No business data model, generic repository, UnitOfWork wrapper, EF InMemory proof, committed credential, or secret logging.
  - **Acceptance criteria:** PostgreSQL 18.4 connection/migration succeeds; readiness includes real DB operation and becomes unhealthy when DB is unavailable; liveness remains distinct.
  - **Verification commands:** explicit migration command; API health requests with PostgreSQL healthy and unavailable; secret-pattern scan of config/evidence.
  - **Required evidence:** Migration list/result, health payload/status/exit, sanitized connection source.
  - **Rollback notes:** Revert bootstrap migration/code; preserve user data/volumes unless task-created disposable resources are explicitly identified.

- [x] 4.3 **BR001-R4.3 Implement deterministic Elsa workflow smoke.**
  - **Objective:** Start and complete a code-defined non-business workflow with observable identity/status/correlation.
  - **Dependencies:** BR001-R4.1–R4.2.
  - **Allowed scope:** Workflow definition/composition and automation-only smoke invocation/result surface.
  - **Forbidden changes:** No product workflow, Elsa authorization, Studio/UI, source modification, background success without awaited terminal state, or secret/PII payload.
  - **Acceptance criteria:** Smoke produces instance ID, terminal completed state, expected deterministic output, and correlation evidence; dependency failure returns non-zero.
  - **Verification commands:** documented workflow smoke command against supported runtime; negative run with required dependency unavailable.
  - **Required evidence:** Instance/status/output/correlation and both exit codes/log locations.
  - **Rollback notes:** Remove only smoke-specific code/config and leave package boundary coherent.

- [x] 4.4 **BR001-R4.4 Implement and prove Aspire and Compose paths.**
  - **Objective:** Run the same API/PostgreSQL/workflow contract through independent developer and demo orchestration.
  - **Dependencies:** BR001-R4.2–R4.3.
  - **Allowed scope:** real AppHost, Dockerfiles, `compose.yaml`, bounded `scripts/smoke-runtime.ps1`, runtime docs.
  - **Forbidden changes:** No external checkout bind/path, mutable `latest`, paid service, hidden dependency between Aspire and Compose, or indefinite startup.
  - **Acceptance criteria:** Compose config validates; each mode starts resources healthy, runs DB/workflow smoke, captures logs/traces, and cleans task-owned processes; image digest recorded.
  - **Verification commands:** `docker compose -f compose.yaml config`; bounded smoke script `-Mode Compose`; bounded AppHost start and smoke script `-Mode Aspire`.
  - **Required evidence:** Resource/health table, startup durations, image tags/digests, smoke results, cleanup state.
  - **Rollback notes:** Stop/remove task-owned containers/processes; delete only explicitly disposable volumes with separate validated authorization.

## BR001-R5 Meaningful Test Foundation

**Beads mapping:** `open_source-cab.4` (all BR001-R5.x tasks). **Dependencies:** BR001-R3 PASS and BR001-R4 PASS.

- [x] 5.1 **BR001-R5.1 Migrate test runner and remove placeholders.**
  - **Objective:** Establish xUnit v3/MTP projects that cannot report green with zero meaningful tests.
  - **Dependencies:** BR001-R3, BR001-R4.
  - **Allowed scope:** retained test project files, `global.json` test runner setting, CPM, removal of empty `UnitTest1` and E2E project.
  - **Forbidden changes:** No xUnit v2 carryover, empty tests, Playwright/browser install, skipped required suite, or test deletion without replacement/evidence.
  - **Acceptance criteria:** Stable xUnit v3 3.2.2 is used; unit/architecture/integration projects discover tests; E2E project is absent and N/A is documented.
  - **Verification commands:** `dotnet test` for each retained project with detailed summary; `rg -n 'xunit"|UnitTest1|E2E' tests DXOS.slnx Directory.Packages.props`.
  - **Required evidence:** Package/test discovery counts and E2E removal/N/A rationale.
  - **Rollback notes:** Revert runner migration if needed, but never restore placeholders as PASS evidence.

- [x] 5.2 **BR001-R5.2 Add unit and architecture rules.**
  - **Objective:** Protect real R4 behavior and approved dependency/ceremony constraints.
  - **Dependencies:** BR001-R5.1.
  - **Allowed scope:** unit/architecture tests and ArchUnitNET 0.13.3 packages/references.
  - **Forbidden changes:** No tests of framework defaults, name-only assertions when binary dependencies can be checked, or weakening production visibility solely for tests.
  - **Acceptance criteria:** Unit tests fail on a real protected behavior regression; architecture tests load production assemblies and enforce direction, framework-free Domain, Elsa NuGet-only boundary, and prohibited patterns; a negative fixture proves rule sensitivity.
  - **Verification commands:** explicit unit and architecture `dotnet test` commands; negative architecture fixture command.
  - **Required evidence:** Test list/count, protected behavior/rules, deliberate negative result.
  - **Rollback notes:** Revert tests/package delta; leave workstream incomplete rather than replace with superficial assertions.

- [x] 5.3 **BR001-R5.3 Add real PostgreSQL and Elsa integration tests.**
  - **Objective:** Exercise migrations, persistence/readiness, and workflow completion on isolated PostgreSQL.
  - **Dependencies:** BR001-R5.1 and R4 runtime.
  - **Allowed scope:** integration fixtures/tests using Testcontainers.PostgreSql 4.13.0 and R4 public/composition boundaries.
  - **Forbidden changes:** No shared developer DB, InMemory provider, mocked database/workflow completion, permanent container, or secret output.
  - **Acceptance criteria:** Container starts, migrations apply, real operation/readiness succeeds, Elsa smoke completes, isolation/disposal works, and Docker absence fails required gate.
  - **Verification commands:** explicit integration test project command; `docker ps -a` before/after; controlled unavailable-Docker/precondition evidence where safe.
  - **Required evidence:** Container image/digest, test count/duration, migration/DB/workflow correlation, cleanup result.
  - **Rollback notes:** Stop/remove only fixture-owned containers; revert integration files/packages without affecting user databases.

- [x] 5.4 **BR001-R5.4 Wire test suites into Runtime gate.**
  - **Objective:** Make suite selection/count/N/A semantics deterministic.
  - **Dependencies:** BR001-R5.1–R5.3.
  - **Allowed scope:** `scripts/check.ps1`, test result output/ignore/docs.
  - **Forbidden changes:** No solution-wide ambiguous test command, zero-count PASS, skipped Docker suite, or E2E PASS.
  - **Acceptance criteria:** Gate calls each project explicitly, validates nonzero counts, propagates failure, and records E2E N/A reason/activation.
  - **Verification commands:** `scripts\check.ps1 -Profile Runtime`; contract verifier test for a deliberately failing/zero-test fixture.
  - **Required evidence:** Per-suite result files/counts and overall exit code.
  - **Rollback notes:** Revert gate wiring and result schema together; do not hide a failing suite.

## BR001-R6 Agent Governance and Project Memory

**Beads mapping:** `open_source-cab.5` (all BR001-R6.x tasks). **Dependencies:** BR001-R5 PASS.

- [x] 6.1 **BR001-R6.1 Recreate DX-OS agent instructions.**
  - **Objective:** Let a fresh agent resolve product, authority, architecture, safety, and workflow without Elsa repository guidance.
  - **Dependencies:** BR001-R5.
  - **Allowed scope:** root `AGENTS.md`, `.agents/rules/**`, approved project skill pointers, encoding cleanup.
  - **Forbidden changes:** No duplicated generated instruction trees, Elsa build/test commands, speculative product rules, authority inversion, or raw chat memory.
  - **Acceptance criteria:** UTF-8 guidance states authority order with the DX-OS constitution above lower project artifacts, OpenSpec/Beads roles, Gemini/Codex protocol, modular-monolith constraints, security/testing/data rules, non-negotiable OSS obligations, truthful OSS-claim language, and the READY business gate.
  - **Verification commands:** encoding validation; `rg` for required terms and forbidden Elsa commands/identity; fresh-agent navigation checklist.
  - **Required evidence:** Instruction inventory, encoding result, authority/path walkthrough.
  - **Rollback notes:** Restore last accepted DX-OS guidance, never old Elsa root instructions.

- [x] 6.2 **BR001-R6.2 Validate OpenSpec, Beads mappings, and bootstrap ADRs.**
  - **Objective:** Align durable contract, execution graph, and architecture memory.
  - **Dependencies:** BR001-R6.1.
  - **Allowed scope:** `openspec/**`, Beads spec links/dependencies/export, bootstrap ADRs.
  - **Forbidden changes:** No competing tracker/spec system, business product spec, closing unreviewed work, or ADR that contradicts accepted OpenSpec.
  - **Acceptance criteria:** Strict OpenSpec validation passes; eight Beads children map bidirectionally with no cycles; the constitution and templates carry mandatory OSS gates; ADRs cover repository extraction, modular monolith, PostgreSQL, Elsa NuGet, Aspire/Compose, the Apache-2.0 default license decision, external-service disclosure, and AI provider independence.
  - **Verification commands:** `openspec.cmd validate --change bootstrap-remediation-001 --strict`; `openspec.cmd status`; `bd.cmd dep tree open_source-cab`; `bd.cmd dep cycles`; spec-link query.
  - **Required evidence:** Validation output, graph/mapping table, ADR index.
  - **Rollback notes:** Restore OpenSpec from Git and Beads from native backup/export; preserve accepted evidence.

- [x] 6.3 **BR001-R6.3 Establish project state and evidence index.**
  - **Objective:** Record only independently accepted engineering truth and next execution state.
  - **Dependencies:** BR001-R6.1–R6.2 and R1–R5 PASS reports.
  - **Allowed scope:** `docs/PROJECT_STATE.md`, evidence index/template, `walkthrough.md` only if evidence-backed.
  - **Forbidden changes:** No aspirational PASS, raw conversation log, duplicate task status, or business-feature start.
  - **Acceptance criteria:** State lists milestone, accepted capabilities, active issue, blockers, decisions/debt, next task, demo readiness, risks, and links to durable evidence; no unverified claim.
  - **Verification commands:** link/path validation; cross-check with `bd.cmd ready --json` and accepted reports; `rg` for required project-state sections.
  - **Required evidence:** Claim-to-evidence matrix and ready-task comparison.
  - **Rollback notes:** Revert state/index to prior accepted snapshot; never infer completion from chat.

## BR001-R7 CI Security OSS Compliance

**Beads mapping:** `open_source-cab.8` (all BR001-R7.x tasks). **Dependencies:** BR001-R6 PASS.

- [ ] 7.1 **BR001-R7.1 Document and verify scanner acquisition/integrity.**
  - **Objective:** Make execution identity trustworthy before scanners see source/secrets.
  - **Dependencies:** BR001-R6.
  - **Allowed scope:** tool manifest/setup documentation, checksum/signature/digest verification, gitignored tool cache policy.
  - **Forbidden changes:** No silent random install, mutable `latest`, affected Trivy 0.69.4/0.69.5/0.69.6, Gitleaks 8.30.1, unpinned Action tag, or committed binary.
  - **Acceptance criteria:** Gitleaks 8.30.0, Trivy 0.72.0, Syft 1.50.0, Grype 0.116.1 identities/licenses/sources are exact and integrity-checked; CI actions use full SHAs.
  - **Verification commands:** each `--version`; checksum/signature/digest verifier; `rg` for forbidden versions/mutable refs.
  - **Required evidence:** Version/integrity/license table and verifier outputs.
  - **Rollback notes:** Remove/quarantine only task-acquired tool cache after validating its path; rotate secrets if compromise evidence exists.

- [ ] 7.2 **BR001-R7.2 Implement scanner canaries, real scans, and SBOM policy.**
  - **Objective:** Prove each tool is effective and produce actionable machine-readable artifacts.
  - **Dependencies:** BR001-R7.1.
  - **Allowed scope:** harmless fixtures, scanner configs, `scripts/verify-security-canaries.ps1`, quality outputs, Syft/Grype/Trivy/Gitleaks commands.
  - **Forbidden changes:** No real secret, ignored expected detection, network/database failure as PASS, empty SBOM, or duplicate vulnerability suppression without rationale/expiry.
  - **Acceptance criteria:** Canaries detect as expected; Gitleaks scans tree/history; Trivy scans repo/config/image; Syft produces nonempty CycloneDX JSON from deliverable; Grype parses/scans it under explicit threshold.
  - **Verification commands:** canary script; real scanner commands from Full gate; SBOM schema/component check; policy-negative fixture.
  - **Required evidence:** Sanitized reports, DB timestamps, component count, thresholds/dispositions, artifact hashes.
  - **Rollback notes:** Remove only synthetic fixtures/output; preserve real findings and do not weaken thresholds to get green.

- [ ] 7.3 **BR001-R7.3 Create DX-OS CI and complete Full gate.**
  - **Objective:** Run local contracts on PR/default branch with bounded, separated, uploaded evidence.
  - **Dependencies:** BR001-R7.2.
  - **Allowed scope:** DX-OS CI workflows, Full gate wiring, artifact retention, CI documentation.
  - **Forbidden changes:** No Elsa workflow copy, mutable third-party action, `continue-on-error` for required gate, hidden CI-only weaker threshold, or push/deploy.
  - **Acceptance criteria:** CI runs restore/format/build/tests/runtime/Compose/Aspire/security/SBOM/OpenSpec checks with same semantics as local Full; it verifies the DX-OS license, attribution, dependency and service disclosures, public-source identity, and absence of undisclosed private-source dependencies; required failure fails the job; artifacts are retained.
  - **Verification commands:** workflow lint/inspection; `scripts\check.ps1 -Profile Full`; authorized CI run result and artifact download/hash checks.
  - **Required evidence:** Workflow SHAs, local/CI parity matrix, run identifiers, job conclusions, artifact hashes.
  - **Rollback notes:** Disable trigger only for verified security incident; keep evidence and earlier gates active.

- [ ] 7.4 **BR001-R7.4 Reconcile OSS, security, and source-license state.**
  - **Objective:** Ensure disclosures and distribution claims match actual resolved deliverables/tools.
  - **Dependencies:** BR001-R7.1–R7.3.
  - **Allowed scope:** `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, `docs/THIRD_PARTY_SERVICES.md`, `SECURITY.md`, `LICENSE`, `NOTICE`, license ADRs, `artifacts/sbom.cdx.json`, dependency approval and reused-source provenance evidence.
  - **Forbidden changes:** No inherited Elsa authorship/license identity, omitted Apache NOTICE duty, concealed copied/adapted/vendored/forked source, unsupported "100% open source" claim, service classified as an OSS dependency, claim for unused dependency, or public release without a valid DX-OS license.
  - **Acceptance criteria:** Apache-2.0 is installed as the DX-OS license unless a verified superseding ADR selects another OSI-compatible license; every direct OSS component and actual transitive/runtime/tool/reused-source dependency is reconciled with component, version, source, license, purpose, modification, redistribution, and attribution state; all third-party services and development AI tools are disclosed separately; the SBOM matches the deliverable.
  - **Verification commands:** compare `dotnet list package --include-transitive`, `artifacts/sbom.cdx.json`, tool manifest, source-provenance inventory, notices, and service disclosure; license/NOTICE link validation; secret scan of evidence; scan public text for prohibited absolute OSS claims.
  - **Required evidence:** Dependency/attribution reconciliation matrix, service disclosure matrix, SBOM path/hash/schema result, license/ADR proof, approval reference without sensitive content, and any unresolved blocker.
  - **Rollback notes:** Revert incorrect disclosure; never erase attribution or fabricate a license.

## BR001-R8 Clean Clone Re-Audit

**Beads mapping:** `open_source-cab.6` (all BR001-R8.x tasks). **Dependencies:** BR001-R7 PASS plus a public DX-OS-owned remote and approved source license.

- [ ] 8.1 **BR001-R8.1 Verify audit prerequisites and create empty clone.**
  - **Objective:** Ensure the audit tests the remote repository, not local overlay/cache state.
  - **Dependencies:** BR001-R7; public owner-approved DX-OS remote and license.
  - **Allowed scope:** Read main new repository/remote/CI; create one validated disposable audit directory and clone.
  - **Forbidden changes:** No audit-time production fix, local file copy into clone, old checkout dependency, remote rewrite, force push, or broad temp deletion.
  - **Acceptance criteria:** Clone parent was empty; remote is public and DX-OS-owned; revision recorded; the repository has its own README, license, release metadata, CI, and reproducible instructions; no Elsa history/source/build inheritance or undisclosed private source; prerequisites/CI available.
  - **Verification commands:** `git clone`; `git -C <clone> rev-parse HEAD`; `git -C <clone> remote -v`; history/tree/reference queries.
  - **Required evidence:** Resolved paths, clone log/revision/remote, independence checks.
  - **Rollback notes:** Keep failed clone for review or remove only the exact validated disposable path with explicit safe cleanup.

- [ ] 8.2 **BR001-R8.2 Execute every READY gate independently.**
  - **Objective:** Produce command/exit/artifact evidence for all required identity/build/runtime/test/security/governance gates.
  - **Dependencies:** BR001-R8.1.
  - **Allowed scope:** Execute documented commands in clean clone; write audit evidence/output.
  - **Forbidden changes:** No skipped required tool, placeholder count, manual substitution, E2E PASS, result editing, or use of old checkout outputs.
  - **Acceptance criteria:** Repository identity, SDK, locked restore, format, Release build, architecture, PostgreSQL/integration, Elsa, Aspire, Compose, DX-OS startup, documented demo, fail-fast, Gitleaks, Trivy, CycloneDX SBOM, Grype, CI, OpenSpec, Beads, agents, DX-OS license, dependency inventory, attribution/notices, third-party-service disclosure, source documentation, release metadata, and project state each have evidence; only UI E2E may be N/A.
  - **Verification commands:** `scripts\verify-check-contract.ps1`; `scripts\check.ps1 -Profile Full`; strict OpenSpec validation; Beads show/cycle query; CI artifact verification.
  - **Required evidence:** Per-gate matrix with command, exit, duration, output path/hash, PASS/FAIL/N/A and rationale.
  - **Rollback notes:** A failure creates narrow upstream remediation and re-runs audit; never edit production during the audit.

- [ ] 8.3 **BR001-R8.3 Publish audit verdict and transition state.**
  - **Objective:** Mark READY only when all required clean-clone evidence passes, then identify (not implement) the first product spec.
  - **Dependencies:** BR001-R8.2.
  - **Allowed scope:** `docs/audits/BOOTSTRAP-AUDIT-002.md`, project state, OpenSpec/Beads task status after Codex verdict.
  - **Forbidden changes:** No READY or competition-release-ready verdict with a missing DX-OS OSS license, incomplete attribution, missing SBOM, undisclosed required service, failed clean-clone source-to-demo path, undisclosed private-source dependency, or other failed/missing/skipped/placeholder gate; no product proposal/implementation before READY; no deletion of old checkout.
  - **Acceptance criteria:** The final CLEAN CLONE / READY audit reports each OSS and competition-submission gate separately; on READY, Beads/OpenSpec/project state align and next proposal is named `identity-organization-audit-foundation`; on failure, blocking items remain open with narrow fixes.
  - **Verification commands:** report completeness checker; compare gate matrix to READY list; `bd.cmd ready --json`; `openspec.cmd status --change bootstrap-remediation-001`.
  - **Required evidence:** Signed-off verdict, state-transition diff, next-task output, old-checkout preservation statement.
  - **Rollback notes:** Revert only incorrect state transitions; audit evidence remains append-only and the old checkout remains backup until separate owner authorization.

## Beads dependency mapping

| OpenSpec workstream | Beads issue | Beads dependency | Blocks |
|---|---|---|---|
| BR001-R1 | `open_source-cab.1` | epic only | BR001-R2 |
| BR001-R2 | `open_source-cab.2` | `open_source-cab.1` | BR001-R3, BR001-R4 |
| BR001-R3 | `open_source-cab.7` | `open_source-cab.2` | BR001-R5 |
| BR001-R4 | `open_source-cab.3` | `open_source-cab.2` | BR001-R5 |
| BR001-R5 | `open_source-cab.4` | `open_source-cab.3`, `open_source-cab.7` | BR001-R6 |
| BR001-R6 | `open_source-cab.5` | `open_source-cab.4` | BR001-R7 |
| BR001-R7 | `open_source-cab.8` | `open_source-cab.5` | BR001-R8 |
| BR001-R8 | `open_source-cab.6` | `open_source-cab.8` | READY |

The Beads issue `spec_id` for each row points to this workstream heading. Each BR001-Rx.y task is executed and reviewed within the mapped issue; no additional tracker or duplicate issue is created unless independent review proves a task cannot fit the workstream contract.
