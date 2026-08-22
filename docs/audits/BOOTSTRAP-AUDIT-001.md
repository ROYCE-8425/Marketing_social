# BOOTSTRAP AUDIT 001

Audit date: 2026-08-11  
Audited branch/commit: `main` at `9bc602ff6` (`main` was one commit ahead of `origin/main`)  
Beads issue: `open_source-apm`  
Scope: repository bootstrap only; no business feature was implemented.

Status terms used below:

- **PASS**: executed and produced evidence that satisfies the stated gate.
- **PARTIAL**: some infrastructure exists, but the required end-to-end behavior was not verified.
- **FAIL**: executed and failed, or the required capability is absent.
- **NOT VERIFIED**: the available evidence is insufficient to make a truthful claim.

# Verdict

## NOT_READY

The bootstrap does not yet meet the DX-OS project constitution. A targeted `dotnet build DXOS.slnx --configuration Release -warnaserror` succeeds, but that proves only that ten mostly empty scaffold projects compile. It does not prove the declared architecture, Elsa integration, Aspire orchestration, PostgreSQL integration, container path, tests, security controls, SBOM, CI, or agent workflow.

Product-feature implementation must remain paused until the P0 fixes in this audit are complete and independently re-audited.

The main blockers are:

1. DX-OS was overlaid on the upstream `elsa-core` repository instead of being bootstrapped as a DX-OS-owned repository.
2. The quality script reports success even when restore, format, build, Docker, Gitleaks, Trivy, and Syft fail.
3. Elsa, Aspire, PostgreSQL, and Docker Compose are not integrated into DX-OS.
4. All four test projects contain one empty test each and provide no meaningful confidence.
5. OpenSpec is empty, ADRs do not exist, `walkthrough.md` is missing, and the root agent instructions still describe Elsa Core.
6. Required security scanners and SBOM tooling are unavailable.

# Critical Findings

## CF-01 — The Git repository is Elsa Core, not a DX-OS-owned repository

**Severity: P0**

Evidence:

- `origin` fetch and push both point to `https://github.com/elsa-workflows/elsa-core.git`.
- The root Git commit is Elsa's 2018 initial commit.
- Recent history before the local Beads bootstrap commit is Elsa Core history.
- The tree contains `Elsa.sln`, 122 non-DXOS project files, the full `src/`, `test/`, `design/`, `docker/`, `doc/`, and build infrastructure from Elsa Core.
- Root `AGENTS.md` and `CLAUDE.md` identify the project as Elsa Core and direct agents toward `Elsa.sln`.
- `Directory.Build.props`, `LICENSE`, package metadata, and repository URLs still identify Elsa Workflows.
- At audit start, every DX-OS solution/project/script/OpenSpec file was untracked; `git ls-files` returned no DX-OS-owned project files.

Impact:

- Repository identity, ownership, release history, CI, package metadata, license presentation, and agent instructions are incorrect for DX-OS.
- Removing Elsa in place would produce a very large deletion-based fork and retain unnecessary Elsa history.
- An accidental push would target the Elsa upstream remote.

## CF-02 — The developer quality gate is false-green

**Severity: P0**

`scripts/check.ps1` has no strict error policy, no command-existence checks, no `$LASTEXITCODE` checks, and no explicit failure propagation. It also omits the solution argument for restore, format, and build.

Observed behavior:

- Normal PowerShell invocation was blocked by the machine execution policy.
- Running with `-ExecutionPolicy Bypass` produced restore/build ambiguity errors, a format exception, a missing Compose-file error, and missing-command errors for Gitleaks, Trivy, and Syft.
- Despite those failures, the script continued and returned exit code `0`.
- Grype is required by `.agents/rules/30-security.md` but is not called by the script.
- OpenSpec validation, E2E, Aspire startup, PostgreSQL connectivity, and runtime smoke checks are not part of the gate.

This script cannot be used as release, review, or agent-completion evidence.

## CF-03 — Bootstrap evidence claimed by the handoff is absent

**Severity: P0**

- `walkthrough.md` does not exist anywhere in the repository.
- `compose.yaml` does not exist.
- `global.json` does not exist.
- `docs/PROJECT_STATE.md` does not exist.
- The ADR, architecture, business, and demo directories are empty; empty directories are not durable Git artifacts.
- There is no DX-OS CI workflow; existing workflows build/test Elsa.
- `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, and `SECURITY.md` are minimal placeholders rather than evidence-backed project documents.

## CF-04 — Passing tests are empty placeholders

**Severity: P0**

Each of the unit, integration, architecture, and E2E projects contains a single empty `Test1` method. The test projects do not reference DX-OS production projects.

- Architecture tests do not reference ArchUnitNET and enforce no boundary.
- Integration tests do not reference Testcontainers or PostgreSQL.
- E2E tests do not reference Playwright.
- Unit tests do not reference domain/application code and contain no assertion.

The commands pass, but the test results are not valid evidence of behavior or architecture.

# Architecture Findings

## Intended architecture

The constitution's target—modular monolith, vertical slices, and selective Clean Architecture—is appropriate. The current scaffold neither violates it with business code nor proves it.

Positive observations:

- `DXOS.slnx` lists only ten DX-OS-named projects and no Elsa projects.
- No generic repository, `UnitOfWork`, empty `IService`/`Service` pair, message broker, or microservice pattern was found in DX-OS source.
- The explicit DX-OS build did not compile Elsa projects.

Blocking observations:

- There are no `ProjectReference` relationships among any DX-OS projects, so no architecture or dependency direction exists yet.
- The architecture test is empty and references no production assembly.
- Application, Domain, Infrastructure, and Workflows projects contain only `Class1`; AppHost is a console template; API is the default weather template.
- Six empty production projects risk turning the initial structure into ceremony before bounded modules and vertical slices have justified the boundaries.
- Root `Directory.Build.props` injects Elsa author/repository/package metadata into DX-OS.
- `src/Directory.Build.props` injects Elsa's `net8.0;net9.0;net10.0` multi-target setting plus Fody and JetBrains annotations into every DX-OS production project.
- Each production project declares `net10.0` locally while also inheriting `TargetFrameworks=net8.0;net9.0;net10.0`. `dotnet list package` consequently evaluates all three frameworks even though the explicit solution build emitted `net10.0` outputs.
- Ten empty `FodyWeavers.xml` files and `<DisableFody>true</DisableFody>` work around inherited Elsa build behavior that DX-OS should not inherit.
- Root `TreatWarningsAsErrors` is `false`; warning enforcement currently depends on remembering a CLI switch.

Rule drift:

- `.agents/rules/00-authority.md` omits the user's explicit current instruction from the top of the authority order, contrary to the master handoff.
- Parts of its Vietnamese text are corrupt even when read explicitly as UTF-8.
- The other rules exist but are too abbreviated to encode the database, migration, idempotency, tenancy, testing, security, and approval constraints in the constitution.
- Root `AGENTS.md` and `CLAUDE.md` remain Elsa Core instructions, so the highest-scope repository guidance directs agents toward the wrong product.

# Elsa Integration

## Classification

| Question | Result | Evidence |
|---|---|---|
| Is the full Elsa source tree present? | **YES** | This checkout is the Elsa Core repository and contains 122 non-DXOS projects. |
| Is Elsa source compiled by `DXOS.slnx`? | **NO** | The explicit DX-OS build compiled only the ten DX-OS projects. |
| Does a DX-OS project reference an Elsa project? | **NO** | No DX-OS `ProjectReference` exists. |
| Does DX-OS consume stable Elsa NuGet packages? | **NO** | No DX-OS project has an Elsa `PackageReference`; built `.deps.json` files contain no Elsa assembly. |
| Is Elsa merely an external reference? | **NO** | Elsa is the owning Git repository, remote, history, root build system, source tree, CI, license, and metadata. |
| Is DX-OS build configuration coupled to Elsa? | **YES** | DX-OS inherits Elsa root/source props, 194 central package-version nodes, multi-targeting, Fody, annotations, metadata, and NuGet feeds. |

Therefore the full Elsa tree is not currently built by `DXOS.slnx`, but DX-OS is still unnecessarily and materially coupled to it. The situation is stronger than normal vendoring: DX-OS is an untracked overlay on Elsa's repository.

## Safest cleanup proposal

Do not delete the Elsa tree in place as the first step. That would create a risky, hard-to-review mass deletion while DX-OS files are still untracked.

Recommended sequence:

1. Freeze feature work and preserve the exact current commit, untracked DX-OS files, and Beads database/export.
2. Create a new DX-OS-owned repository with its own remote and a clean initial history. Prefer this over an orphan commit or a deletion-based Elsa fork.
3. Copy only reviewed DX-OS-owned artifacts: master handoff, agent rules/skills that genuinely apply, OpenSpec, Beads metadata, DX-OS projects/tests, scripts, and audit documents.
4. Recreate minimal DX-OS-owned `Directory.Build.props`, `Directory.Packages.props`, `NuGet.Config`, `.gitignore`, `global.json`, license, and CI. Do not copy Elsa's 194-version package catalog, multi-target props, Fody setup, or feeds by default.
5. Keep `DXOS.slnx` limited to DX-OS-owned projects. Add only intentional project edges supported by an accepted architecture decision.
6. Select the minimum stable Elsa NuGet packages needed by the architecture spike and reference them only from the workflow/composition boundary. Record exact versions and licenses centrally after verifying official package documentation.
7. If source inspection is useful, retain Elsa as a separate sibling clone or an external documentation link pinned to a tag/commit. Do not place it in the DX-OS solution or default build graph.
8. Prove independence from a clean clone: restore, build, meaningful tests, Compose, Aspire, PostgreSQL, Elsa workflow smoke test, scanners, and SBOM must work without the Elsa source checkout.

# Tool Verification

| Tool/capability | Status | Verification result |
|---|---|---|
| OpenSpec | **PARTIAL** | `openspec.cmd --version` returned `1.8.0`; list/validate commands ran. Configuration is the unchanged sample, with no project context, rules, specs, changes, or accepted artifacts. Direct `.ps1` launcher is vulnerable to the machine's PowerShell execution policy. |
| Beads | **PARTIAL** | `bd.cmd` 1.1.2 works in embedded mode and resolves `.beads`. The database had zero issues before this audit; audit issue `open_source-apm` was created and claimed. Plain `bd` selects a blocked PowerShell launcher, and `bd doctor` is unsupported in embedded mode. |
| Context7 MCP | **FAIL** | No callable Context7 tool/server/resource was exposed to this Codex session, and no repository Context7 MCP configuration was found. |
| Docker Engine | **FAIL** | Docker CLI 29.4.2 exists, but the daemon/API pipe is unavailable. |
| Docker Compose | **FAIL** | Compose plugin v5.1.3 exists, but `compose.yaml` is missing and config validation fails. |
| .NET Aspire | **FAIL** | `DXOS.AppHost` has no Aspire hosting package or resource declarations; execution prints `Hello, World!` and exits. No Aspire workload/tool is installed. |
| Gitleaks | **FAIL** | Command not found. |
| Trivy | **FAIL** | Command not found. |
| Syft | **FAIL** | Command not found. |
| Grype | **FAIL** | Command not found and omitted from `scripts/check.ps1`. |
| .NET SDK | **PARTIAL** | .NET SDK 10.0.103 and runtime 10.0.3 are installed, but the repo has no `global.json` pin and inherits contradictory Elsa multi-target configuration. |

# Quality Gate Results

| Gate | Status | Evidence/result |
|---|---|---|
| `scripts/check.ps1` normal invocation | **FAIL** | Blocked by PowerShell execution policy. |
| `scripts/check.ps1` with execution-policy bypass | **FAIL / FALSE-GREEN** | Produced multiple real failures but returned exit code `0`. |
| Explicit restore | **PASS** | `dotnet restore DXOS.slnx` succeeded for 10 projects. |
| Explicit build | **PASS** | `dotnet build DXOS.slnx --configuration Release -warnaserror --no-restore` succeeded with 0 warnings and 0 errors. This is scaffold-only evidence. |
| Formatting | **FAIL** | `dotnet format DXOS.slnx --verify-no-changes --no-restore` exited `2`; ten scaffold files produced eleven whitespace/final-newline violations. |
| Unit tests | **NOT VERIFIED** | Command passed 1/1, but the only test is empty and references no production code. |
| Integration tests | **NOT VERIFIED** | Command passed 1/1, but no PostgreSQL, Testcontainers, application reference, or integration behavior exists. |
| Architecture tests | **NOT VERIFIED** | Command passed 1/1, but no ArchUnitNET rule or production reference exists. |
| E2E tests | **NOT VERIFIED** | Command passed 1/1, but no Playwright/browser/application behavior exists. |
| API process startup | **PARTIAL** | The Release API assembly logged that it was listening on `127.0.0.1:5099`; a weather endpoint smoke request did not return a usable response. The spawned audit process was terminated and no listener was left behind. |
| Docker image build | **FAIL** | No DX-OS Compose file/Dockerfile path is wired into the quality gate; Docker daemon is unavailable. |
| Compose validation | **FAIL** | `compose.yaml` missing. |
| Security scanners | **FAIL** | Gitleaks, Trivy, Syft, and Grype unavailable. |
| NuGet vulnerability audit | **PASS** | Live `dotnet list DXOS.slnx package --vulnerable --include-transitive` found no vulnerable packages from the configured sources. |
| NuGet deprecation audit | **PARTIAL** | The four test projects use legacy/deprecated `xunit` 2.9.3; NuGet recommends `xunit.v3`. |
| SBOM | **FAIL** | Syft is unavailable and `artifacts/sbom.cdx.json` was not produced. |
| Aspire startup | **FAIL** | AppHost is not an Aspire host. |
| PostgreSQL TCP | **PARTIAL** | A local `postgres` process listens on TCP 5432, but DX-OS has no connection string, Npgsql reference, migration, health check, credentials, or executable query test. Application connectivity is **NOT VERIFIED**. |

# Security/OSS Findings

## Security

- A deterministic secrets verdict cannot be issued because Gitleaks is absent. A path-only pattern scan found many expected Elsa security/configuration references; no obvious secret was found in DX-OS `appsettings` files, but this is not equivalent to a scanner pass.
- Root `.gitignore` is Elsa-centric. It ignores build outputs and artifacts, but it does not define a complete DX-OS policy for environment files, certificates, local secrets, generated SBOMs, or deployment overrides.
- No tracked `bin`, `obj`, test-result, DLL, executable, PDB, or package artifact was found. Local ignored `bin`/`obj` directories do exist from verification runs.
- Security policy is a short placeholder and does not document supported deployment, data handling, incident response, AI threat boundaries, or private reporting details specific to DX-OS.
- No tenant isolation, authorization, audit, secret management, dependency review, container scan, or runtime security test exists yet.

## OSS and repository hygiene

- The root MIT license is explicitly Elsa Workflows' license, not a DX-OS licensing decision.
- `OPEN_SOURCE.md` claims Elsa, PostgreSQL, and Aspire use even though DX-OS currently integrates none of them. It omits inherited Fody, JetBrains annotations, xUnit, coverlet, test SDK, and the full Elsa source/dependency context.
- `THIRD_PARTY_NOTICES.md` contains no notices.
- The central package catalog contains 194 package-version nodes copied from Elsa Core; most are not required by DX-OS and have not been approved against the DX-OS dependency policy.
- NuGet configuration includes Elsa/CShells/ConsoleLogStreaming feeds that DX-OS has not justified.
- The repository contains unnecessary Elsa source, tests, docs, Docker files, CI, and large design assets, including tracked files over 5 MiB.
- All current DX-OS bootstrap files remain untracked, so the repository has no reviewable committed DX-OS baseline.

# Technical Debt Created During Bootstrap

1. **Repository identity debt:** DX-OS is mixed into an upstream Elsa clone with the wrong remote, history, ownership metadata, CI, and agent instructions.
2. **Build inheritance debt:** DX-OS carries Elsa multi-targeting, Fody, annotations, feeds, warning policy, and 194 central package versions.
3. **False-confidence debt:** four empty tests and a false-green quality script can be mistaken for a production-grade gate.
4. **Architecture-shell debt:** six empty production projects exist without dependency edges, vertical slices, or executable boundaries.
5. **Operational debt:** Compose, Aspire, PostgreSQL application connectivity, migrations, health checks, logs, traces, and runtime smoke tests are absent.
6. **Security/compliance debt:** scanners, SBOM, notices, threat model, and DX-OS-specific security documentation are absent.
7. **Specification debt:** OpenSpec is unconfigured and empty; ADRs and project state are absent.
8. **Agent-guidance debt:** root instructions describe Elsa; project rules are incomplete and one rule contains corrupted Vietnamese text.
9. **Evidence debt:** `walkthrough.md` is missing, so prior bootstrap claims cannot be traced to commands or repository state.
10. **Tool portability debt:** npm-installed PowerShell launchers for OpenSpec and Beads are blocked by the current execution policy unless `.cmd` is used explicitly.

# Required Fixes

## P0 — Before any product feature

1. **Extract DX-OS into a DX-OS-owned repository.**
   - Acceptance: DX-OS has its own remote/history/metadata; the clean checkout contains no Elsa source tree; `DXOS.slnx` lists only intentional DX-OS projects.
2. **Replace inherited Elsa build/package configuration with minimal DX-OS configuration.**
   - Acceptance: .NET 10 is pinned by `global.json`; production projects target only the intended framework; no inherited Fody/feed/package catalog remains; package metadata identifies DX-OS.
3. **Repair `scripts/check.ps1` as a deterministic fail-fast gate.**
   - Acceptance: it targets `DXOS.slnx`, verifies required commands up front, terminates on the first failed gate with a non-zero exit, calls all required scanners, and can be invoked through a documented Windows-safe command.
4. **Create a real Engineering OS runtime spike.**
   - Acceptance: stable Elsa NuGet packages, PostgreSQL, Aspire, API, and Compose coexist from a clean checkout; no Elsa project reference exists; a workflow and database smoke path are executable.
5. **Replace placeholder tests with meaningful bootstrap tests.**
   - Acceptance: architecture boundaries execute against production assemblies; integration tests execute against PostgreSQL/Testcontainers; E2E is either real or explicitly excluded until a UI exists; empty tests are removed.

## P1 — Before the first accepted product spec is implemented

6. Configure OpenSpec with DX-OS context/rules and create the required ADR set, especially repository architecture, Elsa package strategy, frontend spike, tenancy/authorization, and outbox/event decisions.
7. Replace root `AGENTS.md`/`CLAUDE.md` and expand `.agents/rules` so the authority order and non-negotiable constitution rules are correct and encoding-clean.
8. Add DX-OS CI for restore, format, warning-clean build, meaningful tests, container build, dependency review, Gitleaks, Trivy, Syft/SBOM, and Grype or an approved equivalent.
9. Produce accurate `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, `SECURITY.md`, dependency approval records, and an SBOM from actual resolved dependencies.
10. Add DX-OS-specific `.gitignore`, project state, evidence-backed walkthrough, clean demo scripts, health checks, and runtime verification instructions.
11. Decide whether to migrate from legacy xUnit 2 to xUnit v3 before test infrastructure grows.
12. Re-run BOOTSTRAP AUDIT 001 (or a numbered follow-up audit) from a clean clone and require `READY` before product implementation.

# Recommended First Product Spec

Do not start a product spec until the P0 Engineering OS fixes are accepted and the bootstrap is re-audited.

After that checkpoint, the recommended first product spec is:

## Identity, Organization Boundary, and Audit Foundation

Objective: establish the deterministic security and ownership boundary required by every Lead-to-Revenue, workflow, AI, and analytics slice.

Narrow scope:

- one organization boundary and explicit organization context;
- authenticated user identity;
- deterministic roles/permissions enforced in application code;
- immutable audit records for security-relevant changes;
- health/readiness evidence for API and PostgreSQL;
- minimal admin-facing verification surface only if needed for the competition demo path.

Explicit non-goals:

- no generic repository or UnitOfWork wrapper over EF Core;
- no AI authorization decisions;
- no Elsa workflow for simple deterministic authorization logic;
- no microservices or one-project-per-feature split;
- no broad CRM CRUD;
- no multi-company administration beyond the minimum organization boundary.

Required acceptance evidence should include negative authorization tests, cross-organization isolation tests against real PostgreSQL, architecture tests, audit persistence tests, Compose/Aspire smoke evidence, and a concise security threat model. Lead Intake should become the next vertical spec only after this boundary is proven.
