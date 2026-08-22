# Gemini Implementation Prompt: BR001-R3 Deterministic Quality Gate

You are the primary implementer for BR001-R3 in the independent DX-OS repository.

Repository:

```text
C:\Users\199X\OneDrive\Máy tính\olympic\dx-os
```

Authoritative scope:

- OpenSpec change: `bootstrap-remediation-001`
- OpenSpec tasks: BR001-R3.1, BR001-R3.2, BR001-R3.3
- Beads issue: `open_source-cab.7`
- Expected starting Git revision: `d6b052485cf12843764e93dfc61d4bb9f0570750`
- Expected Beads state: `in_progress`

## Authority and required reading

Before changing any file, read completely:

1. `.specify/memory/constitution.md`
2. `AGENTS.md`
3. all `.agents/rules/**`
4. `openspec/changes/bootstrap-remediation-001/proposal.md`
5. `openspec/changes/bootstrap-remediation-001/design.md`
6. all four specs under `openspec/changes/bootstrap-remediation-001/specs/**/spec.md`
7. the complete `openspec/changes/bootstrap-remediation-001/tasks.md`
8. `scripts/check.ps1`
9. the accepted R2 evidence and verifier under `artifacts/task-runs/open_source-cab.2/`
10. `Directory.Build.props`, `Directory.Packages.props`, `NuGet.Config`, `global.json`, `.gitignore`, and `DXOS.slnx`

Run before mutation:

```powershell
git rev-parse HEAD
git status --short
git remote -v
dotnet --version
openspec.cmd status --change bootstrap-remediation-001 --json
openspec.cmd instructions apply --change bootstrap-remediation-001 --json
bd.cmd show open_source-cab.7 --json
bd.cmd dep cycles
```

Stop and report a blocker if HEAD differs, the worktree contains changes outside this authorized prompt/evidence path, R2 is not closed, R3 is not `in_progress`, OpenSpec is invalid, or the SDK does not resolve normally to `10.0.302`.

Do not modify this `prompt.md` after implementation begins.

## Objective

Replace the false-green `scripts/check.ps1` with a Windows-safe deterministic gate that:

- checks required inputs and tools explicitly;
- invokes native commands using argument arrays without `Invoke-Expression`;
- records the exact target, arguments, exit code, duration, outcome, and sanitized output;
- enforces bounded timeouts and terminates only the process it started;
- stops at the first required failure;
- preserves the original failure and exits non-zero;
- always targets `DXOS.slnx`, explicit project paths, explicit OpenSpec change, and explicit output paths;
- never treats missing or future work as a silent skip or PASS.

Implement all three R3 tasks as one independently reviewable workstream.

## Required profile contract

`scripts/check.ps1` must expose:

```powershell
-Profile Foundation|Runtime|Full
```

`Full` is the default. Profile selection is staged engineering verification, not permission to call a partial profile release-ready.

### Foundation

Foundation must be executable now and must PASS only when all active foundation checks pass. At minimum it must perform:

1. validated repository-root and required-file checks;
2. normal PATH resolution of .NET SDK `10.0.302` without PATH mutation or full-path workaround;
3. hash capture for all nine existing `packages.lock.json` files;
4. `dotnet restore DXOS.slnx --locked-mode`;
5. proof that lock-file set, sizes, and SHA-256 hashes did not change;
6. `dotnet format DXOS.slnx --verify-no-changes --no-restore` or the documented SDK-compatible exact equivalent;
7. `dotnet build DXOS.slnx -c Release --no-restore -warnaserror`;
8. `git diff --check` for the current worktree;
9. strict validation of `bootstrap-remediation-001` using the callable Windows OpenSpec command.

Every native exit code must be asserted. A successful later command must never overwrite an earlier failure.

### Runtime

Runtime must include Foundation plus the declared R4/R5 runtime and meaningful-test gates: explicit unit, architecture, integration, PostgreSQL, Elsa smoke, API health, Aspire, and Docker Compose requirements.

Those capabilities are not implemented yet. They must be represented explicitly as required `NOT_IMPLEMENTED` gates with their activating BR001 task IDs and actionable messages. Runtime must currently exit non-zero at the first required unimplemented gate. It must not execute placeholder tests and call them PASS.

### Full

Full must include Foundation and Runtime plus every READY gate declared by the constitution/OpenSpec, including security scanners, SBOM, OSS/license/service disclosure, governance, CI parity, and clean-clone/release requirements.

Future R4-R8 gates must be present in the manifest/contract now. A required `NOT_IMPLEMENTED` result is a failure. Full must currently exit non-zero at the first downstream required capability that has not been implemented. It must never return zero during R3 merely because later workstreams have not run.

E2E alone is `NOT_APPLICABLE` until a real UI exists. Record its reason and activation condition. Do not install Playwright or create an E2E PASS.

## Manifest and evidence design

Use a deterministic, reviewable check definition. It may live in `scripts/check.ps1` or a narrowly scoped manifest such as `scripts/check-contract.json`, but it must explicitly state for each check:

- stable check ID and owning BR001 task;
- profiles containing the check;
- required versus N/A policy;
- activation state and activation condition;
- required tools and files;
- exact command/arguments or internal assertion;
- timeout;
- expected outputs;
- failure semantics.

Runtime-generated quality outputs must go under a narrow gitignored directory such as `artifacts/quality-gate/`. Do not ignore `artifacts/task-runs/**` or the required future `artifacts/sbom.cdx.json`.

Machine-readable run evidence must include at least schema version, run ID, requested profile, repository root, Git revision, SDK/tool identity where applicable, start/end timestamps, duration, ordered per-check results, exit codes, outcome, output paths/hashes, N/A reason/activation, first failure, and overall result. Never store environment dumps, credentials, tokens, connection strings, or unsanitized secrets.

## Native runner requirements

- Use terminating PowerShell error behavior.
- Do not use `Invoke-Expression`, shell-built command strings, `cmd /c`, or ambiguous auto-discovery.
- Resolve and validate the Git repository root before any cleanup or generated-output operation.
- Do not recursively delete repository paths. Any task-owned temporary path must be a validated strict descendant of the documented output root or a newly created system-temp directory.
- Capture stdout and stderr without deadlocks or truncation.
- Enforce a documented timeout for every native command.
- On timeout, terminate only the process started by the gate, report timeout distinctly, and exit non-zero.
- Preserve native exit codes and useful stderr in the evidence.
- Do not mutate source, project files, package locks, or production configuration during verification.
- Do not install tools, edit host/user PATH, or silently use a private/full executable path.
- Remain compatible with Windows PowerShell 5.1; also work under PowerShell 7 when available.

Narrow helper scripts/modules are allowed only when both `check.ps1` and the contract verifier exercise the same implementation.

## Contract verifier

Create `scripts/verify-check-contract.ps1`. It must exercise actual gate/runner behavior and exit non-zero on any failed assertion. It must prove at least:

1. a missing required tool is detected before dependent checks;
2. a native non-zero exit is preserved and stops later commands;
3. a later success cannot mask an earlier failure;
4. a timed-out command is bounded, terminated, and reported as failure;
5. an unrelated solution file cannot change any selected DX-OS target;
6. command output is not silently truncated;
7. evidence contains exact ordered results and no secret fixture value;
8. Foundation succeeds on the real repository;
9. Runtime and Full fail for the documented first `NOT_IMPLEMENTED` downstream gate;
10. the nine lock files are unchanged by Foundation.

Fixtures must be harmless and synthetic. Create them only under a validated disposable temp/output directory, remove only paths owned by the verifier in `finally`, and never mutate production files to simulate a case. Do not put a real secret in a fixture.

## Allowed implementation scope

Allowed:

- `scripts/check.ps1`
- `scripts/verify-check-contract.ps1`
- narrowly required helpers or `scripts/check-contract.json`
- `.gitignore` only for the exact generated quality-output directory
- a focused developer document such as `docs/QUALITY_GATE.md`
- `artifacts/task-runs/open_source-cab.7/implementation-report.md`
- `artifacts/task-runs/open_source-cab.7/verification.md`
- exact raw outputs and SHA-256 sidecars under the same R3 evidence directory

The existing `artifacts/task-runs/open_source-cab.7/prompt.md` is required evidence and must remain byte-unchanged.

## Forbidden scope

- No R4 runtime implementation or packages: Elsa, EF Core, Npgsql, Aspire, Docker/Compose runtime code.
- No R5 meaningful test implementation or replacement of placeholder tests.
- No R6-R8 implementation, CI workflow, scanner installation, SBOM fabrication, or public visibility change.
- No business feature, API endpoint, domain model, AI provider integration, database model, or UI.
- No changes to `global.json`, CPM, project graph, package locks, LICENSE, constitution, ADRs, or accepted planning content unless an actual contradiction blocks R3; if blocked, stop and report it.
- No writes to the old `open_source` checkout.
- No commit, amend, rebase, history rewrite, merge, remote edit, push, tag, or GitHub publication.
- No Beads close and no additional issue creation unless Codex authorizes it.
- Do not check BR001-R3.1-R3.3 in OpenSpec. The task file explicitly reserves checkbox updates until independent Codex PASS.

## Required verification

Run and record exact commands, exit codes, measured durations, material output, and output hashes:

```powershell
[System.Management.Automation.Language.Parser]::ParseFile(...)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\verify-check-contract.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1 -Profile Foundation
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1 -Profile Runtime
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\check.ps1 -Profile Full
dotnet restore DXOS.slnx --locked-mode
dotnet build DXOS.slnx -c Release --no-restore -warnaserror
openspec.cmd validate bootstrap-remediation-001 --type change --strict --no-interactive
bd.cmd show open_source-cab.7 --json
bd.cmd dep cycles
git diff --check
git status --short
```

Expected results:

- parser checks: success;
- contract verifier: exit 0;
- Foundation: exit 0;
- Runtime: non-zero at the documented first R4/R5 required `NOT_IMPLEMENTED` gate;
- Full: non-zero at the documented first downstream required `NOT_IMPLEMENTED` gate;
- locked restore and Release build: exit 0, zero warnings/errors, locks unchanged;
- OpenSpec strict validation: exit 0;
- Beads: R3 remains `in_progress`, no dependency cycles;
- Git diff hygiene: clean for the submitted changes;
- Git status: only authorized R3 files and evidence.

An expected Runtime/Full non-zero result is evidence of fail-closed staging, not a bootstrap PASS. The overall DX-OS project remains `NOT_READY`.

## Evidence requirements

Write clean UTF-8 Markdown and untruncated raw evidence. At minimum produce:

- `implementation-report.md`: objective, baseline, design, exact changed-file classification, profile contract, safety choices, limitations, and rollback;
- `verification.md`: complete command/result matrix with exit codes, durations, expected versus actual outcomes, exact first failures, lock hashes before/after, artifact hashes, final Git/Beads/OpenSpec state;
- separate raw outputs for the contract verifier, Foundation success, Runtime expected failure, and Full expected failure;
- SHA-256 sidecars or a final checksum table generated after outputs are frozen.

Do not embed a self-referential hash inside the file being hashed. Do not abbreviate output with ellipses. Mark genuinely unavailable historical data as `UNAVAILABLE`; never invent a timing, exit code, hash, test result, or command output.

## Handoff

Return to Codex with:

- exact changed-file list;
- contract/profile design summary;
- every command and exit code;
- Foundation PASS evidence;
- Runtime and Full expected-failure evidence;
- verifier and evidence hashes;
- unresolved limitations/blockers;
- declarations that the old checkout was untouched, R3 remains `in_progress`, OpenSpec R3 checkboxes remain unchecked, and no commit/push/remote/business/R4+ work occurred.

Do not describe BR001-R3 as accepted or complete. Only Codex can issue `PASS` or `FIX_REQUIRED` after independent inspection.
