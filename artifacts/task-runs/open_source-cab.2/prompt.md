# Gemini Implementation Prompt: BR001-R2 Minimal DX-OS Build System

Task: `open_source-cab.2`
OpenSpec scope: BR001-R2.1 through BR001-R2.4
Repository: `C:\Users\199X\OneDrive\Máy tính\olympic\dx-os`

## Objective

Recreate the smallest deterministic DX-OS-owned .NET 10 build/package/project foundation. Do not implement business behavior or any BR001-R3+ runtime, quality-gate, security, CI, or product work.

## Authority

Read before mutation:

1. `AGENTS.md` and all `.agents/rules/**`
2. `openspec/changes/bootstrap-remediation-001/tasks.md`, BR001-R2.1 through BR001-R2.4
3. `openspec/changes/bootstrap-remediation-001/plan.md`, R2
4. `openspec/changes/bootstrap-remediation-001/design.md`, decisions 3 through 5
5. `openspec/changes/bootstrap-remediation-001/research.md`
6. repository-foundation and quality-evidence delta specs
7. `bd.cmd show open_source-cab.2 --json`

OpenSpec defines the contract; Beads owns execution state. Claim `open_source-cab.2` before implementation. Do not close it; Codex closes it only after independent PASS.

## Current State to Preserve

- BR001-R1 passed at extraction commit `4a1f8db0c657e65716280def51e869357acbfa02`.
- Codex then closed R1 and checked BR001-R1.1 through R1.4 complete. Preserve the resulting post-PASS modifications to `.beads/interactions.jsonl` and `openspec/changes/bootstrap-remediation-001/tasks.md`.
- No remote is configured and the source license remains pending. Do not invent either.
- Do not copy any root build/package configuration from the old Elsa checkout.

## Required Implementation

### BR001-R2.1 — SDK and root policy

- Create DX-OS-owned `global.json` pinning SDK `10.0.302`, `rollForward: latestPatch`, and `allowPrerelease: false`.
- Recreate `Directory.Build.props`, `Directory.Build.targets`, and `.editorconfig` with `net10.0` only, nullable and implicit usings enabled, deterministic builds, stable SDK language default/C# 14 policy, and Release warnings-as-errors.
- Use DX-OS metadata only. No Elsa authorship, URLs, icons, suppressions, preview language, multi-targeting, blanket `NoWarn`, or automatic SDK installation.
- Ensure `.gitignore` covers generated build/test/tool output without hiding required evidence or source.

### BR001-R2.2 — CPM and feeds

- Create minimal `Directory.Packages.props` with central package management enabled and only packages actually referenced by retained projects.
- Create minimal `NuGet.Config` using `<clear />` and stable NuGet.org only.
- No Feedz/private/preview feeds, floating versions, per-project package versions, copied Elsa catalog, or unused dependencies.
- Document the lock-file policy. Generate and review lock files if locked restore is selected; exit criteria require deterministic restore behavior.
- Reconcile actual direct/transitive package versions and licenses against research. Do not add Elsa runtime packages merely for R2; Elsa integration belongs to BR001-R4 unless compilation genuinely requires an approved stable package and OpenSpec is coherently updated first.

### BR001-R2.3 — Project direction

- Keep only coarse DX-OS boundaries justified by the design; do not create a project per feature.
- Enforce this intended production direction without cycles:
  - `DXOS.Domain` -> no DX-OS project
  - `DXOS.Application` -> `DXOS.Domain`
  - `DXOS.Workflows` -> `DXOS.Application`
  - `DXOS.Infrastructure` -> `DXOS.Application`, `DXOS.Domain`
  - `DXOS.Api` -> `DXOS.Application`, `DXOS.Infrastructure`, `DXOS.Workflows`
  - `DXOS.AppHost` -> only intentional hosting references
- Remove Fody files/package remnants and other inherited/generated artifacts.
- Configure retained test projects only with stable xUnit v3/Microsoft Testing Platform foundations justified for later R5 work. Do not claim placeholder tests as meaningful.
- Add a deterministic project-graph verifier sufficient to reject cycles, Elsa source references, and paths back to `open_source`.
- No generic repository, UnitOfWork wrapper, empty IService/Service pair, microservice, broker/cache scaffold, or business code.

### BR001-R2.4 — Clean restore/build proof

- Validate absolute paths before removing only new-repository `bin`/`obj` outputs.
- Restore only `DXOS.slnx`; establish the documented lock policy, then run locked restore when applicable.
- Build `DXOS.slnx` Release with warnings as errors and no restore.
- Prove no generated build output is tracked and report the post-run working-tree state, distinguishing authorized R2 changes from generated artifacts.

## Required Verification

Record literal commands, exit codes, measured durations, material outputs/output paths, and SHA-256 hashes. At minimum run:

```powershell
dotnet --version
dotnet --info
dotnet nuget list source
dotnet msbuild DXOS.slnx -getProperty:TargetFramework -getProperty:TreatWarningsAsErrors
dotnet restore DXOS.slnx --locked-mode
dotnet build DXOS.slnx -c Release --no-restore -warnaserror
dotnet sln DXOS.slnx list
dotnet list DXOS.slnx reference
dotnet list DXOS.slnx package --include-transitive
rg -n 'elsa-workflows|feedz|f\.feedz\.io|Version=' Directory.Build.props Directory.Build.targets Directory.Packages.props NuGet.Config src tests
rg -n --glob '*.csproj' 'Elsa|\.\.[\\/].*open_source' .
git status --short
```

Also run the project-graph verifier, tracked-artifact scan, package-version centralization check, UTF-8/control-character check for recreated root files, and strict OpenSpec validation.

If `--locked-mode` cannot be used before lock files exist, perform the documented first restore required to generate/review them, then require the final locked restore to pass. Missing SDK, unavailable NuGet, restore errors, warnings, or absent tools must fail explicitly; do not silently skip or suppress.

## Evidence

Create in the new repository:

- `artifacts/task-runs/open_source-cab.2/implementation-report.md`
- `artifacts/task-runs/open_source-cab.2/verification.md`
- deterministic verifier script(s) and sanitized output files needed to reproduce the claims

Reports must distinguish build foundation PASS from later NOT_READY capabilities. Unit/integration/architecture/runtime/security/Aspire/Compose gates are not R2 PASS evidence unless explicitly required above.

## Prohibitions

- Do not modify the old `open_source` checkout.
- Do not implement BR001-R3 or later workstreams.
- Do not merge, push, add a remote, rewrite R1 history, or commit unless separately authorized.
- Do not close Beads or check BR001-R2 tasks complete; Codex performs state transition only after review PASS.
- Do not weaken OpenSpec to match an implementation shortcut.

## Handoff

Return:

- changed/created files;
- exact command/result matrix;
- SDK, feed, package, project-graph, restore, and Release-build results;
- warnings and unresolved blockers;
- explicit declarations: old checkout untouched, no merge, no push, no business feature.

Codex will inspect the actual repository and issue `PASS` or `FIX_REQUIRED`.
