# Implementation Plan

This plan implements the requirements in `proposal.md`, `design.md`, `research.md`, and `specs/**`. OpenSpec owns the contract; the Beads epic `open_source-cab` owns execution state. Workstream identifiers are stable and map one-to-one to Beads children in `tasks.md`.

## Dependency order

```text
BR001-R1 Repository Extraction
            |
            v
BR001-R2 Minimal DX-OS Build System
            |
            +------------------------+
            |                        |
            v                        v
BR001-R3 Quality Gate       BR001-R4 Runtime Spike
            |                        |
            +-----------+------------+
                        v
              BR001-R5 Test Foundation
                        |
                        v
             BR001-R6 Agent Governance
                        |
                        v
          BR001-R7 CI / Security / OSS
                        |
                        v
           BR001-R8 Clean Clone Re-Audit
                        |
                        v
                      READY
```

R3 and R4 may be implemented in parallel after R2, but R5 cannot start until both receive independent PASS. No business feature or the deferred product spec may start before R8 returns READY.

## R1 — Repository Extraction

**Beads:** `open_source-cab.1`

**Objective**

Create a new DX-OS-owned repository from a reviewed inventory without deleting, rewriting, or repurposing the Elsa checkout.

**Inputs**

- Accepted `docs/audits/BOOTSTRAP-AUDIT-001.md`.
- `DXOS_CODEX_MASTER_HANDOFF.md` and this OpenSpec change.
- Current Git revision/remotes/status and DX-OS candidate files.
- Current Beads epic/children and supported export/backup commands.
- Design decisions 1–2.

**Files likely affected**

- Old checkout: only `artifacts/task-runs/open_source-cab.1/**`, reviewed Beads export/backup metadata, and issue state.
- New repository: `src/DXOS.*/**`, `tests/DXOS.*/**`, `scripts/**` only when classified DX-OS-owned, `docs/**`, `openspec/**`, `.agents/**`, `.beads/**` created through supported migration, and recreated root metadata.
- Inventory: `artifacts/task-runs/open_source-cab.1/inventory.csv` and `extraction-manifest.json`.

**Implementation steps**

1. Record old checkout absolute path, `git rev-parse HEAD`, branches, remotes, status, and hashes of all candidate DX-OS artifacts.
2. Export Beads regular issues to reviewed JSONL and create/sync a Dolt-native filesystem backup; record `bd version` and backup status. Do not copy live locks or embedded DB files blindly.
3. Classify every candidate as `copy`, `recreate`, or `exclude`, with owner/provenance and reason. Exclude all `bin/`, `obj/`, logs, locks, caches, Elsa source/projects, Elsa solution/build/release files, upstream generated integrations, and inherited root configuration.
4. Resolve the new target, default `../dx-os`, to an absolute path. Fail before writing if it is inside the old checkout, equals the old checkout, is not empty, or already contains `.git`/user data.
5. Create the new directory, copy only manifest-approved artifacts, verify copied SHA-256 values, and recreate root identity/configuration placeholders called out by design rather than copying Elsa files.
6. Initialize a new Git repository with a DX-OS default branch. Do not add a remote unless a Product Owner-approved non-Elsa URL is supplied. Never push in this task.
7. Initialize a new `dxos` Beads database and restore/import the reviewed graph through supported commands. Verify the epic, eight children, dependencies, and spec links.
8. Prove no Elsa source tree, project reference, Git history, remote, build script, preview feed, generated output, or inherited package catalog entered the new repository.
9. Write `implementation-report.md` and `verification.md` in the old checkout and copy the same durable evidence to the new repository.

**Verification**

```powershell
git -C <old-checkout> rev-parse HEAD
git -C <old-checkout> status --short
git -C <new-repository> rev-parse --show-toplevel
git -C <new-repository> log --oneline --decorate -1
git -C <new-repository> remote -v
git -C <new-repository> status --short
git -C <new-repository> ls-files
dotnet sln <new-repository>\DXOS.slnx list
rg -n --glob '*.csproj' 'ProjectReference|Elsa' <new-repository>
rg --files <new-repository> | rg '(^|[\\/])(bin|obj|src[\\/]modules[\\/]Elsa|Elsa\.sln|build)([\\/]|$)'
bd.cmd -C <new-repository> show open_source-cab --json
bd.cmd -C <new-repository> dep cycles
```

The verification report must distinguish a deliberately absent remote (`REMOTE_PENDING_OWNER_AUTHORIZATION`) from an accidentally inherited Elsa remote. R1 may receive PASS for safe local extraction with remote pending, but R8 cannot receive READY until an approved DX-OS remote supports a clean clone.

**Rollback/recovery strategy**

Stop; do not delete either directory. The old checkout remains authoritative backup. If the target is invalid, quarantine the incomplete newly created target by renaming it only with user approval, then retry into a new empty path using the saved manifest and Beads backup. Never roll back by rewriting Elsa history.

**Dependencies**

None beyond the accepted audit/change and accessible current checkout.

**Exit criteria**

- Reviewed inventory and hashes exist.
- Old checkout/revision/remotes remain unchanged by extraction.
- New repository has independent Git/Beads identity and all authoritative planning/evidence.
- No accidental Elsa source/build/history/remote or generated artifact is present.
- `open_source-cab.1` receives Codex PASS.

**Risks**

- Copy omission, destructive target selection, live Beads database corruption, accidental upstream remote reuse, or copying inherited root configuration.

## R2 — Minimal DX-OS Build System

**Beads:** `open_source-cab.2`

**Objective**

Recreate the smallest deterministic .NET 10 solution/package/build foundation owned by DX-OS.

**Inputs**

- R1 repository and extraction manifest.
- Research version/license matrix.
- Repository-foundation spec and design decisions 3–5.

**Files likely affected**

- `global.json`, `Directory.Build.props`, `Directory.Build.targets`, `Directory.Packages.props`, `NuGet.Config`, `.editorconfig`, `.gitignore`.
- `DXOS.slnx`, selected `src/DXOS.*/**/*.csproj`, selected `tests/DXOS.*/**/*.csproj`.
- Root `README.md`, package/repository metadata, and R2 evidence.

**Implementation steps**

1. Add the exact SDK/test-runner policy from research; reject previews and unsupported feature bands.
2. Recreate root build properties with DX-OS authorship/repository metadata, nullable/implicit usings, deterministic builds, analyzers, and Release warnings-as-errors. Do not copy Elsa suppressions, trimming/package/release settings, icons, or URLs.
3. Recreate CPM with only packages actually used at this stage. Remove private/preview feeds and source mappings not needed for stable NuGet.org packages.
4. Review each placeholder production project. Keep only coarse boundaries required by the design; add explicit project references matching the approved graph and no circular reference.
5. Recreate test project SDK/runner configuration using stable xUnit v3/MTP only where tests are retained for later work. Remove Fody files and inherited package artifacts.
6. Ensure `DXOS.slnx` names only intentional DX-OS projects. Do not add Elsa or old-repository paths.
7. Add a minimal architecture-graph assertion or deterministic project-reference validation sufficient to protect the foundation before R5 adds full ArchUnitNET rules.
8. Restore and build from a clean package/output state; capture resolved packages and warnings.

**Verification**

```powershell
dotnet --version
dotnet nuget list source
dotnet restore DXOS.slnx --locked-mode
dotnet build DXOS.slnx -c Release --no-restore -warnaserror
dotnet sln DXOS.slnx list
dotnet list DXOS.slnx reference
dotnet list DXOS.slnx package --include-transitive
rg -n 'elsa-workflows|feedz|Version=' Directory.Build.props Directory.Packages.props NuGet.Config src tests
git status --short
```

If locked restore is introduced during the task, generate/review lock files first and then run the shown locked command; otherwise the first restore may run without `--locked-mode`, but exit criteria require deterministic lock policy documented for CI.

**Rollback/recovery strategy**

Revert only R2 changes in the new repository or restore the R1 snapshot/branch. The old checkout is untouched and must not be copied over the new build configuration as a shortcut.

**Dependencies**

- R1 PASS.

**Exit criteria**

- The selected 10.0.302 SDK is enforced.
- Restore and Release build target only `DXOS.slnx` and pass without inherited Elsa configuration.
- CPM/feed catalog is minimal and version/license-reviewed.
- Project direction has no cycles, Elsa source reference, or speculative infrastructure.
- `open_source-cab.2` receives Codex PASS.

**Risks**

- Over-preserving empty projects, package-lock drift, local SDK mismatch, suppressing warnings instead of fixing them, or accidentally reintroducing Elsa feeds.

## R3 — Deterministic Quality Gate

**Beads:** `open_source-cab.7`

**Objective**

Replace `scripts/check.ps1` with a Windows-safe fail-fast gate whose required stages, targets, tools, timeouts, outputs, and exit behavior are executable and testable.

**Inputs**

- R2 build commands/targets.
- Quality-evidence spec and design decision 10.
- Final gate list from the accepted request.

**Files likely affected**

- `scripts/check.ps1`, `scripts/verify-check-contract.ps1`, optional narrowly scoped helper scripts/modules.
- `.gitignore`, `docs/DEVELOPMENT.md`, task evidence.

**Implementation steps**

1. Define profiles `Foundation`, `Runtime`, and `Full`; default is `Full`. Profiles explicitly enumerate required checks and may not silently skip them. `NOT_APPLICABLE` is allowed only for E2E with a recorded reason/activation condition.
2. Use terminating PowerShell errors and a native-command invocation helper that captures command, duration, stdout/stderr path, and `$LASTEXITCODE`, then throws/returns non-zero immediately.
3. Preflight every executable/config/input needed by the selected profile before dependent work. Report exact missing tool/version and official setup guidance.
4. Name `DXOS.slnx`, test projects, `compose.yaml`, OpenSpec change, and evidence output paths explicitly.
5. Add bounded timeouts and cleanup/finally behavior for processes/containers started by runtime checks.
6. Create a contract verifier that runs the gate in isolated temporary fixtures to prove: missing tool fails; a native command failure propagates; later success cannot mask failure; ambiguous solution selection is impossible.
7. Wire the Foundation profile to R2 restore/format/build and OpenSpec validation. Add declared placeholders for downstream stages that fail as “not implemented” if selected before R4/R5/R7, rather than returning PASS.
8. Document supported Windows PowerShell invocation and evidence schema.

**Verification**

```powershell
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts\verify-check-contract.ps1
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1 -Profile Foundation
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1 -Profile Full
```

At R3, `Full` is expected to fail non-zero at the first not-yet-implemented required downstream gate with an explicit reason. That failure is evidence that requirements are not silently skipped, not a Foundation failure.

**Rollback/recovery strategy**

Revert R3 scripts/docs only. Retain the audit copy of the old false-green script as historical evidence under the audit/task report, never as an executable fallback.

**Dependencies**

- R2 PASS.

**Exit criteria**

- Contract verifier proves fail-fast/missing-tool/native-exit behavior.
- Foundation passes and Full truthfully fails until downstream gates exist.
- No required check can be silently skipped or redirected to another solution.
- `open_source-cab.7` receives Codex PASS.

**Risks**

- PowerShell native exit handling, process timeout leaks, profiles becoming a bypass, or evidence files leaking environment secrets.

## R4 — Engineering Runtime Spike

**Beads:** `open_source-cab.3`

**Objective**

Prove the DX-OS runtime path with stable Elsa NuGet, PostgreSQL, Aspire, Compose, health, and a deterministic workflow smoke—without product behavior.

**Inputs**

- R2 build foundation.
- Engineering-runtime spec.
- Research decisions 2–5 and design decisions 6–8.

**Files likely affected**

- `src/DXOS.Workflows/**`, `src/DXOS.Infrastructure/**`, `src/DXOS.Api/**`, `src/DXOS.AppHost/**`.
- `Directory.Packages.props`, project files, migrations bootstrap, `compose.yaml`, Dockerfiles, runtime scripts/docs.
- R4 prompt/report/verification evidence.

**Implementation steps**

1. Add only approved stable direct packages through CPM: Elsa bundle, EF Core/Npgsql, Aspire AppHost/PostgreSQL, and required Microsoft health/observability packages.
2. Configure a minimal Infrastructure `DbContext`/migration boundary and a readiness health check that performs a real database operation. No repository/UnitOfWork wrappers.
3. Define a deterministic code-first Elsa smoke workflow with a known observable completion result, instance ID/status, and correlation identifier.
4. Compose the runtime in API startup; expose liveness/readiness and a bootstrap-only smoke invocation surface suitable for automation without introducing product endpoints.
5. Convert AppHost into a real Aspire host that declares PostgreSQL 18.4/database, references API, waits for health, and exposes structured logs/traces.
6. Create Dockerfile(s) and `compose.yaml` with explicit health, secrets via variables, named volumes, bounded dependencies, and no external checkout paths. Record resolved image digests.
7. Add `scripts/smoke-runtime.ps1` or equivalent for `Aspire` and `Compose` modes with cleanup/finally semantics and non-zero failure.
8. Update runtime/OSS docs for actual dependencies only.

**Verification**

```powershell
dotnet restore DXOS.slnx --locked-mode
dotnet build DXOS.slnx -c Release --no-restore -warnaserror
docker compose -f compose.yaml config
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-runtime.ps1 -Mode Compose
dotnet run --project src\DXOS.AppHost\DXOS.AppHost.csproj --no-build
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-runtime.ps1 -Mode Aspire
rg -n --glob '*.csproj' 'Elsa|ProjectReference' src
```

Startup commands must be run through the bounded verifier, not left blocking indefinitely. Evidence records resource health, database operation, workflow terminal status, and correlation without credentials.

**Rollback/recovery strategy**

Stop/remove only task-owned containers and volumes documented as disposable; preserve named data unless the task explicitly created it for the smoke. Revert R4 code/config/package changes in the new repository. Do not fall back to Elsa source.

**Dependencies**

- R2 PASS. R3 may run in parallel but must pass before R5.

**Exit criteria**

- Both Aspire and Compose paths can start the same API/PostgreSQL/workflow smoke contract independently.
- Readiness fails when PostgreSQL is unavailable and succeeds only after a real operation.
- Elsa appears only as stable NuGet dependency and the workflow completes observably.
- `open_source-cab.3` receives Codex PASS.

**Risks**

- Blocking AppHost processes, hidden local secrets, container tag drift, smoke endpoints growing into product API, or Elsa transitive compatibility errors.

## R5 — Meaningful Test Foundation

**Beads:** `open_source-cab.4`

**Objective**

Replace every placeholder with tests that can fail on a real regression and connect them to the deterministic gate.

**Inputs**

- R3 gate contract and R4 production runtime.
- Research decisions 6–9.
- Quality-evidence spec.

**Files likely affected**

- `tests/DXOS.Unit.Tests/**`, `tests/DXOS.Architecture.Tests/**`, `tests/DXOS.Integration.Tests/**`.
- Removal of `tests/DXOS.E2E.Tests/**` placeholder.
- CPM/project files, `scripts/check.ps1`, test docs/evidence.

**Implementation steps**

1. Migrate retained test projects to xUnit v3/MTP and assert nonzero test discovery/execution.
2. Add focused unit tests for real production behavior from R4 (workflow result mapping, health/readiness decision, or other deterministic non-I/O logic); no tests of framework defaults.
3. Add ArchUnitNET rules for the approved project/type direction, framework-free Domain, infrastructure boundaries, Elsa NuGet-only use, and prohibited generic repository/UnitOfWork/service-pair patterns where executable.
4. Add Testcontainers PostgreSQL tests that start 18.4 (or approved digest), apply migrations, execute a real write/read/readiness contract, and clean up.
5. Add an Elsa workflow smoke assertion that verifies a real instance reaches the expected terminal state with correlation evidence.
6. Remove empty tests and the E2E project from disk/solution/package catalog; encode E2E as `NOT_APPLICABLE` in gate/report with activation condition.
7. Wire explicit unit, architecture, and integration commands into the gate. Fail when zero tests are executed or Docker is unavailable for required integration tests.

**Verification**

```powershell
dotnet test tests\DXOS.Unit.Tests\DXOS.Unit.Tests.csproj -c Release --no-build
dotnet test tests\DXOS.Architecture.Tests\DXOS.Architecture.Tests.csproj -c Release --no-build
dotnet test tests\DXOS.Integration.Tests\DXOS.Integration.Tests.csproj -c Release --no-build
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1 -Profile Runtime
rg -n 'UnitTest1|Assert\.True\(true\)|E2E' tests DXOS.slnx Directory.Packages.props
```

Evidence includes executed/passed/failed/skipped counts per suite and a deliberate negative test demonstrating at least one architecture rule fails when its fixture violates the boundary.

**Rollback/recovery strategy**

Revert R5 tests/package/gate wiring only. Never restore empty placeholders as evidence; if a suite cannot be made meaningful, leave the workstream incomplete.

**Dependencies**

- R3 PASS and R4 PASS.

**Exit criteria**

- All three required suites exercise production code/rules with meaningful assertions and nonzero counts.
- PostgreSQL tests use a real isolated engine.
- Empty E2E artifacts are removed and status is truthful.
- `open_source-cab.4` receives Codex PASS.

**Risks**

- Testing framework setup reports zero tests, architecture rules test only names instead of dependencies, Docker flakiness, or smoke tests coupling to implementation details.

## R6 — Agent Governance and Project Memory

**Beads:** `open_source-cab.5`

**Objective**

Make DX-OS intent, decisions, execution state, agent authority, and accepted evidence durable and navigable in the new repository.

**Inputs**

- Accepted OpenSpec artifacts and R1–R5 evidence.
- Agent-governance spec.
- Codex → Gemini and review protocol in the master handoff.

**Files likely affected**

- `AGENTS.md`, `.agents/rules/**`, approved `.agents/skills/**` pointers.
- `openspec/config.yaml`, this change, ADRs, `docs/PROJECT_STATE.md`, `walkthrough.md` if retained.
- `.beads` supported configuration/export and task evidence indexes.

**Implementation steps**

1. Recreate concise DX-OS root agent guidance in valid UTF-8; remove Elsa-specific repository/build instructions and generated duplication.
2. Encode the authority order, scope discipline, security/database/testing rules, OpenSpec contract, Beads workflow, Gemini implementer/Codex reviewer separation, and business-work READY gate.
3. Ensure OpenSpec config resolves repository-local context and validates this change; do not introduce SpecKit as a competing source of truth.
4. Create only bootstrap-relevant ADRs: modular monolith, PostgreSQL, Elsa via NuGet, Aspire/Compose roles, and repository extraction/ownership. Defer product-only ADRs.
5. Create/update `docs/PROJECT_STATE.md` from accepted PASS evidence, including current milestone, open issues, blockers, debt, next task, demo readiness, and risks.
6. Verify all eight Beads issues have spec links and dependency graph matches `tasks.md`; export reviewed issue state for migration/recovery.
7. Add an evidence index and enforce the prescribed task-run filenames; do not store raw conversations.
8. Keep `walkthrough.md` absent or explicitly evidence-backed; no aspirational PASS statements.

**Verification**

```powershell
openspec.cmd validate --change bootstrap-remediation-001 --strict
openspec.cmd status --change bootstrap-remediation-001
bd.cmd show open_source-cab --json
bd.cmd dep tree open_source-cab
bd.cmd dep cycles
rg -n 'Elsa Core is|Elsa\.sln|build\.sh Test' AGENTS.md .agents
rg -n 'OpenSpec|Beads|Gemini|Codex|READY' AGENTS.md .agents docs\PROJECT_STATE.md
```

**Rollback/recovery strategy**

Restore the last accepted DX-OS guidance/config from Git and the Beads backup. Do not copy old Elsa `AGENTS.md` back into the new root.

**Dependencies**

- R5 PASS.

**Exit criteria**

- A fresh agent can find authority, active change, next Beads task, gates, and prohibitions without Elsa instructions.
- OpenSpec validates and Beads mapping/dependencies are coherent.
- ADR/project state/evidence reflect only independently accepted work.
- `open_source-cab.5` receives Codex PASS.

**Risks**

- Duplicated/stale rules, invalid encoding, aspirational project state, or Markdown checklists becoming a parallel tracker.

## R7 — CI / Security / OSS Compliance

**Beads:** `open_source-cab.8`

**Objective**

Make local and CI supply-chain/security/OSS claims executable, pinned, licensed, and evidence-producing.

**Inputs**

- R3 full-gate framework and R4/R5 deliverables.
- R6 governance/evidence contracts.
- Research decisions 10–13 and quality-evidence spec.

**Files likely affected**

- `.github/workflows/**` or approved CI equivalent.
- `scripts/check.ps1`, scanner config/policy, `artifacts` output rules.
- `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, `SECURITY.md`, license decision/status.
- SBOM/report output paths and R7 evidence.

**Implementation steps**

1. Define tool bootstrap documentation for exact Gitleaks 8.30.0, Trivy 0.72.0, Syft 1.50.0, and Grype 0.116.1 with checksum/signature/digest verification. Do not download tools silently inside a scan.
2. Add harmless canaries: Gitleaks must detect a synthetic secret fixture; Trivy must detect a controlled insecure fixture/policy case; Syft must produce a nonempty expected component; Grype must parse the SBOM and honor a controlled policy result.
3. Configure real scans: Gitleaks working tree plus history; Trivy repository/config and built image; Syft CycloneDX JSON from actual deliverable; Grype scan of that SBOM with explicit severity/fix threshold.
4. Wire scanners and SBOM into the Full quality profile. Missing tools, stale/unavailable required databases, invalid signatures, absent output, or scan policy failure are non-zero.
5. Create DX-OS CI that uses the current .NET pin, PostgreSQL/Docker prerequisites, same explicit gate contracts, separate result groups, bounded runtime, and uploaded evidence.
6. Pin all third-party actions to full commit SHAs and annotate the intended version. Never use Trivy mutable tags/action refs or affected releases.
7. Generate/review resolved dependency/license inventory. Update notices for only actual shipped packages/tools/copying, including MIT, Apache-2.0, and PostgreSQL obligations and any NOTICE content.
8. Apply the Product Owner-approved Apache-2.0 DX-OS license decision, reconcile attribution/notices/SBOM/service disclosure, and never reuse Elsa authorship/license identity.

**Verification**

```powershell
gitleaks version
trivy --version
syft version
grype version
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts\verify-security-canaries.ps1
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1 -Profile Full
Test-Path artifacts\quality\sbom.cdx.json
rg -n 'uses:.*@(main|master|v[0-9]|latest)' .github\workflows
rg -n '0\.69\.(4|5|6)|8\.30\.1|latest' .github scripts compose.yaml
```

CI verification additionally records workflow/run URLs or immutable run identifiers, individual job conclusions, and artifact hashes.

**Rollback/recovery strategy**

Disable only newly introduced CI triggers if they expose a verified security risk, keeping the workflow file/evidence for review. Revert R7 policy/config without weakening earlier correctness gates. Rotate any potentially exposed secrets if scanner supply-chain compromise evidence appears.

**Dependencies**

- R6 PASS.

**Exit criteria**

- Full local and CI gates execute effective pinned scanners and generate/scan an SBOM.
- CI/local semantics and failure thresholds align.
- OSS/security/source-license records match actual deliverables and distribution.
- `open_source-cab.8` receives Codex PASS.

**Risks**

- Scanner setup compromise, vulnerability DB nondeterminism, Action tag mutation, organization licensing mistakes, noisy thresholds, or incomplete NuGet license extraction.

## R8 — Clean Clone Re-Audit

**Beads:** `open_source-cab.6`

**Objective**

Independently prove the new remote repository reaches every READY condition from an empty clone with no access to the Elsa checkout or untracked local state.

**Inputs**

- R1–R7 Codex PASS evidence.
- Product Owner-approved DX-OS remote and source license.
- Final Full quality gate and READY checklist.

**Files likely affected**

- `docs/audits/BOOTSTRAP-AUDIT-002.md`.
- `docs/PROJECT_STATE.md`, task evidence, Beads/OpenSpec status after verdict.
- No production changes are allowed during the audit; defects create/fix upstream tasks and require re-run.

**Implementation steps**

1. Verify the remote is DX-OS-owned and contains no Elsa Core history/reference except documented NuGet attribution.
2. Create a new empty temporary parent and clone by remote URL. Record clone revision, environment/tool versions, and prove the old checkout path is not referenced or required.
3. Run repository identity, SDK, restore, format, Release build, architecture, unit/integration, PostgreSQL, Elsa smoke, Aspire, Compose, gate-contract, Gitleaks, Trivy, Syft, Grype, CI, OpenSpec, Beads, agent, OSS, and project-state checks separately.
4. Inspect actual test counts, scanner canaries, command exit codes, health/workflow/database evidence, SBOM components, notices, and CI artifacts. Do not accept implementer narratives.
5. Publish BOOTSTRAP AUDIT 002 with PASS/FAIL/NOT_APPLICABLE per gate. Any required skipped/missing/placeholder/failing gate prevents READY.
6. If READY, close/update Beads/OpenSpec task status and project state; then and only then identify `identity-organization-audit-foundation` as the next product proposal. Do not implement it.

**Verification**

```powershell
git clone <approved-dxos-remote> <empty-temp>\dx-os-clean
git -C <empty-temp>\dx-os-clean rev-parse HEAD
git -C <empty-temp>\dx-os-clean remote -v
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File <empty-temp>\dx-os-clean\scripts\verify-check-contract.ps1
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File <empty-temp>\dx-os-clean\scripts\check.ps1 -Profile Full
openspec.cmd -C <empty-temp>\dx-os-clean validate --change bootstrap-remediation-001 --strict
bd.cmd -C <empty-temp>\dx-os-clean show open_source-cab --json
```

If a CLI does not support `-C`, execute it with the clean clone as the process working directory rather than changing the command target semantics.

**Rollback/recovery strategy**

Delete only the explicitly created disposable audit clone after recording evidence and only with verified absolute-path safety. A failed audit does not modify or delete the main new repository or old Elsa checkout; it creates narrow remediation work and repeats R8.

**Dependencies**

- R7 PASS, approved remote configured, CI run available, and source license resolved.

**Exit criteria**

- BOOTSTRAP AUDIT 002 returns `READY` with every required gate PASS and only UI E2E truthfully N/A.
- The new clone is independent of Elsa source/history/build state.
- Beads/OpenSpec/project state are coherent and business work is explicitly unblocked.
- `open_source-cab.6` and epic `open_source-cab` receive Codex PASS/closure.

**Risks**

- Local cache contamination, inaccessible/private remote, CI-only discrepancies, unrecorded manual prerequisites, or treating unavailable scanners/runtime as skips.
