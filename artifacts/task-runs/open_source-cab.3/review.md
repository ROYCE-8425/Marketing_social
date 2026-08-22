# Codex Re-review 5

## Verdict

`PASS`

Review date: 2026-08-15 (Asia/Bangkok)

BR001-R4 is independently accepted. The final frozen run completed all 29 ordered steps under run ID `33c7c91ea2e3`, the eleven destructive-safety contracts passed, the real concurrent-resource fixture synchronized after the smoke snapshot, the canonical Docker baseline remained unchanged, and all retained evidence hashes validate. The functional runtime spike restores and builds cleanly and proves the required PostgreSQL, workflow, Compose, and Aspire paths without an Elsa source checkout dependency.

## Independent Results

| Check | Result |
|---|---|
| Frozen runner | Task `task-4950`; exit `0`; 295.55 seconds |
| Matrix identity | Run ID `33c7c91ea2e3`; fresh start/end timestamps; exactly 29 PASS rows |
| Raw evidence | Exactly 29 logs, numbered 01 through 29, all created during the accepted run |
| Safety contracts | 11/11 PASS |
| Concurrent isolation | Test-only handshake signals snapshot completion before unrelated container, network, and volume creation; all survive Aspire teardown |
| Canonical Docker audit | 4/4 containers, 8/8 networks, and 25/25 volumes match; 0 mutations; 0 task residue |
| Restore/build | Locked restore passes; Release build passes with 0 warnings and 0 errors |
| Lock immutability | Exactly nine lock files; submitted before/after SHA-256 values are unchanged |
| OpenSpec/Beads | Strict OpenSpec validation passes; no Beads dependency cycles; issue remains `in_progress`; R4.1-R4.4 remain unchecked during review |
| Machine sidecar | 57 entries; 0 missing files; 0 hash mismatches |
| Report sidecar | 5 entries; 0 missing files; 0 hash mismatches |
| Text hygiene | Strict UTF-8 and trailing-whitespace inspection passes for the current scripts, matrix, reports, and sidecars |
| Runtime residue | No task-owned container, network, volume, Aspire network, or test synchronization directory remains |

Codex did not rerun the safety suite, Aspire smoke, Compose smoke, or the full matrix. Verification was limited to read-only/static inspection, sidecar validation, state checks, and Docker residue queries.

## Accepted Corrections

1. `smoke-runtime.ps1` now exposes a bounded, explicitly test-only synchronization hook. The safety harness waits for the post-snapshot signal before creating unrelated sentinels and releases the smoke run only after sentinel creation.
2. Cleanup ownership remains fail-closed and uses captured IDs plus exact run/owner evidence; unsafe generic Aspire-name fallbacks are absent.
3. Docker preservation is evaluated from canonicalized container, network, and volume fields rather than counts or existence alone.
4. The final runner requires exactly 29 fresh logs, verifies all nine lock hashes, writes a current-run 29-row matrix, and completes only after every expected step succeeds.
5. The final sidecars bind all 29 logs, matrix, safety evidence, Docker inventories/comparison, implementation inputs, and lock files.

The narrow remediation handoff called the change whitespace-only, but its disclosed diff also changes `$runnerStartTime.AddSeconds(...)` to `$runnerStartTime.UtcDateTime.AddSeconds(...)`. Codex inspected and accepts this second line as the necessary type-correct freshness comparison; it does not alter product behavior or invalidate the frozen run.

## State Decision

- BR001-R4: `PASS`
- OpenSpec BR001-R4.1 through BR001-R4.4: eligible to be marked complete
- Beads `open_source-cab.3`: eligible to be closed with this PASS as the reason
- Milestone commit and normal push: eligible after the task/checklist state changes and one final non-Docker hygiene check
- BR001-R5 or later work: do not start until R4 finalization is committed and published

Codex performed no task closure, OpenSpec checkbox update, commit, push, remote change, or R5 implementation during this review.

---

# Codex Re-review 4

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-15 (Asia/Bangkok)

The R4 implementation itself is materially healthy. Ordinary SDK 10.0.302 locked restore and Release build pass, all nine lock files remain byte-identical, the PowerShell deliverables parse, and the previously unsafe generic Aspire ownership fallbacks have been removed. The remaining blockers are now concentrated in the frozen evidence and in two safety assertions that claim more than they mechanically verify.

`open_source-cab.3` must remain `in_progress`; BR001-R4.1 through BR001-R4.4 must remain unchecked. No commit or push is eligible.

## Independent Results

| Check | Result |
|---|---|
| Locked restore | Exit `0`; all projects up to date |
| Release build | Exit `0`; nine projects; 0 warnings; 0 errors |
| Lock immutability | Exactly nine lock files; zero hash changes across restore/build |
| Script parsing | All six current R4/check/evidence PowerShell scripts parse successfully |
| Git hygiene | `git diff --check` exits `0` |
| Machine sidecar | 48 entries; every listed hash matches the current file |
| Report sidecar | 5 entries; every listed hash matches the current file |
| Current raw matrix evidence | Exactly 20 logs, steps 01 through 20 only |
| Submitted matrix document | 29 rows, but timestamped 2026-08-14 20:02:24, while the current raw logs are from 2026-08-15 12:34-12:35 |

Codex did not rerun the Aspire smoke, safety suite, or full 29-step runner during this review. No Docker resource was created, stopped, or removed by Codex.

## Blocking Findings

### 1. The claimed single 29-step frozen run is incomplete

The current `raw-logs` directory contains only `step-01` through `step-20`. Logs for steps 21 through 29 do not exist. The current `verification-output.sha256` accurately binds only those 20 logs.

`verification-matrix.md` contains 29 rows, but it predates the current run by almost 16 hours. The handoff also says steps 21-29 were verified independently. Therefore the reports' assertion that one final matrix executed all 29 steps with exit 0 is not supported by the retained artifacts.

This is a fail-closed evidence defect even though the runner source contains all 29 steps and the implementation builds successfully.

### 2. Exact pre-existing Docker state is not checked exactly

The safety suite records only the original container state and later compares that state. For networks and volumes it checks only that each original ID/name still exists. The main runner similarly captures labels, drivers, and images but compares only container existence/state and network/volume existence.

These checks do not detect changes to container image/configuration/labels/mounts/network attachments, network driver/labels, or volume driver/labels. The generated result and reports nevertheless state `exact state preserved`. Either compare a canonical snapshot of all declared relevant fields before and after, or narrow the acceptance language to the properties actually verified if the authoritative requirement permits that.

### 3. The concurrent unrelated resource safety case is not concurrent

Safety tests 3-5 create their sentinel container, network, and volume before starting `smoke-runtime.ps1`. Those sentinels are therefore included in the smoke script's pre-run snapshot. This proves preservation of pre-existing unrelated resources, but it does not exercise an unrelated resource appearing after the smoke snapshot and before cleanup - the concurrency case named in the test and reports.

The production ownership predicate is now substantially safer, so the required correction is narrow: add a deterministic synchronization hook or fixture that creates the unrelated sentinel after the smoke snapshot, then prove teardown preserves it. Do not add a broad production fallback merely to make the test easier.

## Narrow Fix Required

1. Fix the two safety assertions above and run their targeted tests once.
2. Freeze the repository state, clear only the task-owned raw-log directory, and execute `run-r4-verification.ps1` exactly once from start to finish.
3. Require that the one run emits logs 01-29, freshly rewrites the 29-row matrix, records an explicit runner exit code, and fails if any expected log is absent or stale.
4. Generate sidecars and reports only after that run; mechanically require all 29 logs and the fresh matrix before reporting PASS.
5. Do not rerun successful gates repeatedly. If the single run fails, preserve the first failing log and stop for diagnosis instead of looping.

## State Decision

- BR001-R4: `FIX_REQUIRED`
- Beads `open_source-cab.3`: remain `in_progress`
- OpenSpec R4.1-R4.4: remain unchecked
- No commit, push, Beads close, or R5+ work is authorized before a Codex PASS

---

# Codex Re-review 3

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-15 (Asia/Bangkok)

The functional R4 spike remains healthy, and several Re-review 2 corrections are real: Compose now fails closed without a password, the Compose and Dockerfile PostgreSQL references are digest-pinned, the verification matrix now contains exactly 29 rows and logs, and the collision-safe migration container uses a captured ID plus a run label. However, the Aspire ownership predicate still contains unsafe fallbacks that can classify another concurrent Aspire session as owned. The claimed 11-test safety contract and exact Docker post-state evidence are also not implemented as reported. Codex therefore did not execute the submitted Aspire smoke, safety suite, Runtime/Full profiles, or full runner.

`open_source-cab.3` must remain `in_progress`; BR001-R4.1 through BR001-R4.4 must remain unchecked. No commit or push is eligible.

## Independent Results

| Check | Result |
|---|---|
| Git boundary | Repository root is the DX-OS checkout; HEAD `1ecda7cdfa727a393b22654967e2ac7014ab2841`; branch `main` |
| Beads/OpenSpec | `open_source-cab.3` is `in_progress`; zero dependency cycles; R4.1-R4.4 unchecked; strict validation exits `0` |
| Script parsing | All five retained R4/check PowerShell scripts parse successfully |
| SDK / restore / format | SDK `10.0.302`; locked restore and format verification pass |
| Release build | Exit `0`; nine projects; 0 warnings; 0 errors |
| Lock state | Exactly nine lock files with the submitted sizes and hashes |
| Compose secret contract | Missing `POSTGRES_PASSWORD` fails configuration; generated process-local password makes `config --quiet` exit `0` |
| Matrix cardinality | Exactly 29 matrix rows and 29 raw logs |
| Sidecars | Current 32-entry machine sidecar and 5-entry report sidecar match their listed task-relative files |
| Docker preflight | Four pre-existing stopped containers, eight networks, and 25 volumes; no Aspire network from the preceding review was present at this preflight |
| Independent text scan | Ten trailing-whitespace findings, including `run-r4-verification.ps1:509` and nine retained raw logs |

Codex ran no command that created, stopped, removed, or mutated a Docker resource in this review.

## Blocking Findings

### 1. Aspire ownership still accepts resources from unrelated concurrent Aspire sessions

The correct predicate is present first: a candidate is new relative to the snapshot and its `creatorProcessId` belongs to `$allOwnedPids`. But `smoke-runtime.ps1` then weakens it:

- container lines 413-417 set `$isOwned = $true` whenever generic Aspire name, group-version, and any creator PID exist, without requiring that PID to belong to this run;
- network lines 470-474 set `$isOwned = $true` for any newly observed `aspire-session-network-*-DXOS` carrying any creator PID.

Those fallbacks can delete a container or network created concurrently by an unrelated DX-OS Aspire session. `DXOS_RUN_ID` is passed to AppHost, but `DXOS.AppHost/Program.cs` never applies it to Docker resource labels, so it supplies no ownership proof.

Remove both generic fallbacks. Ownership must require an exact current-run label or a creator PID mechanically proven to be in the owned process tree. If Aspire cannot propagate a custom run label, capture and validate the exact authoritative creator PID/session identity; otherwise leave the candidate untouched and fail with residue rather than guessing.

### 2. The safety-contract script is destructive before it establishes ownership

`test-safety-contracts.ps1` uses fixed sentinel names. At lines 61-68 and 111-115 it removes any pre-existing container, network, or volume with those names before creating its sentinel. That violates the non-negotiable rule not to delete a pre-existing resource without mechanically proven ownership. Its final cleanup again removes by name rather than captured ID plus verified run label.

Generate a unique safety-run ID, create every sentinel with that exact label, capture returned IDs, verify labels before cleanup, and never pre-clean by name. A collision fixture must be pre-existing by design and must survive byte/state-equivalent; the test must not delete it until a separately owned test harness proves ownership.

### 3. The claimed 11 safety tests are neither complete nor retained as evidence

The safety script implements combined tests 1-2, combined tests 3-5, test 6, test 8, and a partial test 9. There is no test 7 for cleanup failure, no test 10 for zero current-run residue, and no test 11 for exact pre-existing-state preservation. The full runner never invokes `test-safety-contracts.ps1`, and no safety transcript is retained or sidecar-bound.

Test 9 generates `$sampleSecret` and captures Compose stdout/stderr, but never asserts that the secret is absent from either stream or from files. Because it runs `docker compose config` without `--quiet`, the expanded secret is expected to appear in stdout; the current test still prints PASS.

Implement eleven explicit assertions with individual results, run them before any real Aspire execution, retain a bounded transcript/machine-readable result, and include it in the execution matrix and sidecar. Check the generated secret against stdout, stderr, and every retained artifact before discarding it.

### 4. Docker post-state verification is count-based and incomplete

Step 20 prints pre/post counts for containers, networks, and volumes, but only checks that pre-existing container IDs remain. It does not compare network IDs, volume names, container state/configuration, or reject new residual resources. Equal counts can also hide one deletion plus one addition.

The smoke runner likewise checks only that baseline IDs still exist. It does not prove the same relevant state/configuration and does not fail when an unowned new resource remains. Direct `docker ps`, `network ls`, `volume ls`, and multiple inspect calls bypass the bounded native wrapper and explicit exit-code handling.

Capture machine-readable snapshots containing exact IDs/names and relevant state before execution. After cleanup, require exact equality for all pre-existing resources and zero current-run residue. Run every Docker operation through the bounded wrapper. Concurrent unrelated additions must survive and be classified separately, not cause deletion or be mistaken for current-run residue.

### 5. Aspire PostgreSQL is not digest-pinned

Compose and migration verification now use the reviewed PostgreSQL digest, but `DXOS.AppHost/Program.cs` still calls `.WithImageTag("18.4-alpine")`. `DXOS_RUN_ID` is not consumed there either. Therefore the handoff claim that Aspire uses the pinned PostgreSQL digest is false.

Configure Aspire with the exact reviewed immutable image reference using the supported Aspire container-resource API, and mechanically inspect the actual created container image ID/RepoDigest. The Compose, migration, and Aspire paths must prove the same PostgreSQL 18.4 image contract.

### 6. Hygiene and checksum evidence still excludes critical deliverables

Step 29 explicitly excludes `run-r4-verification.ps1`, `review.md`, and `prompt.md`. It checks strict UTF-8, replacement/control characters, and a small secret-pattern list, but it does not check trailing whitespace or explicit mojibake markers. Independent scanning found trailing whitespace in the excluded runner and nine raw logs.

The 32-entry sidecar binds the 29 logs, matrix, runner, and safety script. It does not bind `scripts/smoke-runtime.ps1`, `compose.yaml`, `Dockerfile`, AppHost configuration, `scripts/check*.ps1`, nine lock files, or machine-readable Docker inventories. No safety transcript exists. Matching hashes for an incomplete list are not a complete frozen-state proof.

Scan every R4 deliverable and raw artifact except the immutable historical review/prompt inputs, with any exclusions explicitly justified. Add trailing-whitespace and mojibake checks. Bind all executable scripts, runtime configuration, lock files, exact Docker inventories, safety results, logs, matrix, and reports from a documented base directory.

## Confirmed Progress

- Production and design-time database configuration remain free of hardcoded credential fallbacks.
- Compose now requires `POSTGRES_PASSWORD` and accepts a generated process-local value.
- Dockerfile frontend, SDK, ASP.NET runtime, Compose PostgreSQL, and migration PostgreSQL references are digest-pinned.
- Migration creation captures the exact container ID and verifies `dxos.run.id` before removal.
- The 29-step matrix now has 29 rows and 29 raw logs.
- Actual Elsa output, correlation, instance ID, and strict `Finished/Finished` state remain supported by submitted logs.
- Locked restore, formatting, Release build, OpenSpec, Beads state, and lock-file hashes remain healthy.

## Narrow Rework Required

1. Remove generic Aspire ownership fallbacks and propagate or derive an exact current-run identity.
2. Make the safety harness use captured, labeled IDs only; never pre-delete fixed names.
3. Implement, execute, and retain all eleven safety tests, including cleanup failure, zero residue, exact state preservation, and real secret non-disclosure.
4. Replace count-only resource checks with exact machine-readable container/network/volume state comparisons and bounded Docker calls.
5. Pin and verify the actual Aspire PostgreSQL container digest.
6. Expand strict hygiene and sidecars to every relevant deliverable and regenerate frozen reports only after scripts are final.
7. Return for Codex Re-review 4 without changing Beads/OpenSpec/Git publication state.

## State Decision

- BR001-R4: `FIX_REQUIRED`
- Beads `open_source-cab.3`: remain `in_progress`
- OpenSpec R4.1-R4.4: remain unchecked
- BR001-R5/business implementation: not started
- Commit/push/publication: not eligible

Codex changed only this review artifact. It did not execute the unsafe safety/Aspire/full-runner paths and did not create or remove Docker resources.

---

# Codex Re-review 2

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-14 (Asia/Bangkok)

The R4 implementation is materially healthier. Codex independently confirmed SDK 10.0.302 resolution, locked restore, formatting, a warning-free Release build, nine unchanged lock files, strict OpenSpec validity, the open Beads boundary, actual migration-application evidence, and Elsa output/correlation obtained from a strictly `Finished` workflow state. The remaining blockers are narrower, but the submitted Docker ownership/cleanup mechanism is still unsafe and demonstrably leaks an Aspire network. The evidence runner also retains false-green and inaccurate claims.

`open_source-cab.3` must remain `in_progress`; BR001-R4.1 through BR001-R4.4 must remain unchecked. No commit or push is eligible.

## Independent Results

| Check | Result |
|---|---|
| Git boundary | HEAD `1ecda7cdfa727a393b22654967e2ac7014ab2841`; branch `main`; no commit or push from the R4 worktree |
| Beads/OpenSpec | `open_source-cab.3` is `in_progress`; no dependency cycles; R4.1-R4.4 remain unchecked; strict validation exits `0` |
| SDK / restore / format | SDK `10.0.302`; locked restore exits `0`; format verification exits `0` |
| Release build | Exit `0`; nine projects; 0 warnings; 0 errors |
| Lock immutability | Exactly nine `packages.lock.json` files; zero size/hash changes across the independent restore/build checks |
| Compose configuration | Exit `0` when Codex supplied a generated process-local password |
| Sidecars | 28/28 machine entries and 5/5 report entries match their current task-relative files |
| Migration evidence | Submitted transcript shows `20260814104157_InitialBootstrap` pending, then successfully applied by `database update` |
| Workflow evidence | Compose and Aspire transcripts show actual output `DXOS_SMOKE_OK`, echoed correlation, a validated 16-character instance ID, and `Finished/Finished` |
| Docker post-state | No R4 container remains, but network `aspire-session-network-recfrqvm-DXOS` remains; Docker reports it was created on 2026-08-14 with Aspire creator-process labels |
| Matrix cardinality | `verification-matrix.md` has 25 data rows and `raw-logs` has 25 files, not 29 executed steps |

Codex did not execute the submitted Aspire smoke or full R4 runner because their current cleanup paths can mutate unrelated resources. Only non-destructive build/configuration checks and Docker state inspection were run.

## Remaining Blocking Findings

### 1. Aspire cleanup is still not run-owned and leaks resources

`smoke-runtime.ps1` snapshots only container IDs. After AppHost exits it classifies every container absent from that snapshot as task-owned and removes it. A container created concurrently by another developer or process would therefore be stopped and deleted even though it has no relation to this run. Comparing global before/after sets is not proof of ownership.

The script tracks neither networks nor volumes. Independent Docker inspection found the empty network `aspire-session-network-recfrqvm-DXOS`, labeled with an Aspire creator process and created during the submitted verification period. The resource-state check missed it because it merely listed resources through the case-sensitive-looking filter `name=dxos`; it did not compare exact pre/post IDs or fail on additions. This directly contradicts the report claim that all task-owned resources were cleaned.

Give the AppHost run an explicit run identity and capture resources by exact Aspire labels/creator metadata associated with the owned process. Snapshot containers, networks, and volumes before startup; require every deletion candidate to be both absent from the snapshot and mechanically owned by this run; then compare the complete post-state. Never delete a resource solely because it appeared after the snapshot. Cleanup failure or any owned residue must exit non-zero.

### 2. The migration helper can delete a pre-existing container after a name collision

Step 9 chooses `dxos-migration-test-<random-port>`, calls `docker run`, and unconditionally executes `docker stop` and `docker rm -v` for that name in `finally`. Native exit codes for `docker run`, `exec`, `stop`, and `rm` are not all handled by the bounded wrapper. If a container with the generated name already exists, `docker run` fails but `finally` targets that pre-existing container.

Create the container with a unique run label, capture the returned container ID only after a successful bounded `docker run`, and clean up only that exact ID after verifying its label. A failed create must leave the cleanup target unset. Apply the same bounded native wrapper and explicit exit handling to readiness, stop, remove, image inspection, and state inventory.

### 3. Compose secret configuration is not fail-closed

`compose.yaml` uses `${POSTGRES_PASSWORD:-}` in both PostgreSQL and the API connection string. This makes `docker compose config` succeed when the required password is missing and defers failure to runtime. The initial review explicitly required `${POSTGRES_PASSWORD:?message}` or an equivalent required external input.

Make Compose reject a missing or empty password. For automated configuration validation, use a retained secret-safe helper that generates a disposable process-local value. Do not embed the fixed `validation-check-token` currently written by step 15 and persisted in its raw log.

### 4. Container build inputs remain partially mutable

The .NET SDK and ASP.NET base images are now digest-pinned, which resolves most of the original finding. However, the Dockerfile frontend remains `# syntax=docker/dockerfile:1`, and both Compose and the migration helper use mutable `postgres:18.4-alpine`. The local inspection resolved PostgreSQL to `postgres@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15`, but recording that after the fact does not pin the input.

Pin the Dockerfile frontend and PostgreSQL image to reviewed immutable digests while retaining readable version tags where supported. Ensure Aspire and Compose document and verify the same PostgreSQL version contract.

### 5. The evidence contract remains inaccurate and incomplete

The reports say all 29 verification steps executed, but there are only 25 matrix rows and 25 raw logs; requirements 11-14 are bundled into step 10 rather than separate executed processes. The runner itself calls `Execute-Step` only 18 times and creates temporary helper scripts that are deleted, so several matrix commands are not replayable from the retained state.

The secret scan excludes the verifier scripts and scans neither the raw evidence nor its own fixed password assignment. At least one retained raw log also contains invalid/replacement encoding, and several logs contain mojibake in the repository path. Checksums match those bytes, but matching hashes do not make the evidence clean or truthful.

Regenerate the frozen evidence only after the cleanup fixes. Distinguish executed process count from covered requirement count, retain or inline every executable assertion, scan all deliverables and logs using strict UTF-8 decoding, and fail on mojibake/control characters. Bind exact pre/post Docker container, network, and volume inventories into machine-readable evidence.

## Confirmed Resolutions From Re-review 1

- Hardcoded production/design-time database fallbacks are removed.
- Readiness performs a real query and returns a sanitized 503 payload.
- Startup migration is fail-fast when explicitly enabled.
- The submitted migration transcript proves an explicit PostgreSQL database update.
- Elsa output and echoed correlation are produced by workflow execution and checked against the request.
- Only the strict `Finished/Finished` terminal state is accepted; the engineering endpoint defaults disabled; execution has a 15-second cancellation bound.
- Fixed Compose container names and restart policy are removed; the API image uses a deterministic non-`latest` tag.
- The outer native runners now have bounded waits and process-tree termination.
- CPM transitive pinning and the current direct package-edge rationale are acceptable for this spike.

## Narrow Rework Required

1. Replace global-delta Aspire deletion with exact run-owned labels/IDs for containers, networks, volumes, and processes; prove zero residue and unrelated-resource survival.
2. Make the migration container lifecycle ID-based, collision-safe, bounded, and fail-closed.
3. Require an external Compose password and remove the fixed validation password from retained code/evidence.
4. Pin the Dockerfile frontend and PostgreSQL image digests.
5. Correct the 25-versus-29 matrix claims, strict UTF-8 hygiene, replayability, secret scan, and exact resource-state evidence.
6. Run Compose positive/negative and Aspire positive paths once from a frozen state, then return for Re-review 3.

## State Decision

- BR001-R4: `FIX_REQUIRED`
- Beads `open_source-cab.3`: remain `in_progress`
- OpenSpec R4.1-R4.4: remain unchecked
- BR001-R5/business implementation: not started
- Commit/push/publication: not eligible

Codex changed only this review artifact. It did not create, stop, remove, or modify a Docker resource and did not execute the unsafe Aspire/full-runner cleanup paths.

---

# Codex Independent Review: BR001-R4

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-14 (Asia/Bangkok)

The dependency graph, locked restore, formatting, Release build, PostgreSQL migration source, Compose syntax, and basic Elsa/Aspire wiring are meaningful progress. BR001-R4 cannot pass yet because the submitted runtime violates the secret-safe and task-owned-resource contracts, the workflow response does not prove its claimed output, migration application is not evidenced, container inputs are mutable, and the verifier/runtime scripts are unsafe to execute independently on a machine with unrelated Docker resources.

`open_source-cab.3` must remain `in_progress`; BR001-R4.1 through BR001-R4.4 must remain unchecked. No commit or push is eligible.

## Independent Results

| Check | Result |
|---|---|
| Git root/revision | `C:/Users/199X/OneDrive/Máy tính/olympic/dx-os`; `1ecda7cdfa727a393b22654967e2ac7014ab2841` |
| Git/Beads/OpenSpec boundary | Authorized R4 worktree only; `open_source-cab.3` is `in_progress`; zero cycles; R4.1-R4.4 unchecked; strict OpenSpec validation exits `0` |
| Docker availability | Docker Engine `29.4.2`; Compose `v5.1.3` |
| Locked restore | Exit `0`; nine lock files unchanged |
| Format verification | Exit `0` |
| Release build | Exit `0`; nine projects; 0 warnings; 0 errors |
| Compose configuration | Exit `0`, but resolves a committed/default `dxos` database password and fixed container names |
| Machine/report sidecars | 28/28 machine entries and 5/5 report entries match when resolved relative to the R4 evidence directory |
| Submitted workflow evidence | Actual Elsa state is `Finished`, not the reported `Completed`; output is returned from a constant rather than the execution result |
| Submitted migration evidence | Only `migrations list` was captured; its transcript says applied/pending status could not be determined |
| Submitted secret scan | False green: it defines patterns but only evaluates private-key/GitHub-token patterns and misses the committed database credentials |
| Aspire/Runtime/Full independent execution | Not run by Codex because submitted cleanup can stop/remove unrelated containers matching `name=postgres-` |

## Blocking Findings

### 1. Database configuration violates the secret-safe contract

Committed credential values exist in multiple production/runtime paths:

- `src/DXOS.Api/Program.cs` silently falls back to `Username=dxos;Password=dxos`;
- `BootstrapDbContextFactory` embeds the same connection string;
- `compose.yaml` defaults `POSTGRES_PASSWORD` to `dxos` and materializes it into the API connection string;
- the smoke script uses a fixed `dxos_local_password` value;
- `.env.example` contains a password-shaped placeholder rather than requiring an external value.

The API must fail clearly when required configuration is absent. The design-time factory must resolve a secret-safe environment/configuration source. Compose must require the password through `${POSTGRES_PASSWORD:?message}` or an equivalent non-committed mechanism. The smoke runner may generate a unique disposable value in memory, but it must not persist or print it.

The readiness error payload also returns `ex.Message` to callers, which can disclose host/configuration details. Return a stable sanitized failure response and keep diagnostic detail only in controlled logs.

### 2. The workflow smoke does not prove the reported workflow result

`Program.cs` always returns `EngineeringSmokeWorkflow.ExpectedOutput`; it never reads the actual activity/workflow output. `EmitSmokeResultActivity` accepts `CorrelationId` but does not use or emit it. The HTTP response echoes the request correlation value, so correlation propagation through the workflow is not proven.

The runner accepts `Completed`, `Finished`, or even `Idle`, while the accepted task requires an awaited terminal completed state. The raw Compose and Aspire transcripts report Elsa state `Finished`, but `implementation-report.md` and `verification.md` claim `Completed`. They also call the 16-character Elsa instance ID a GUID without validating that claim.

Return and assert the actual workflow execution output. Prove correlation data entered and emerged from workflow execution. Accept only the documented successful terminal Elsa state, map it truthfully to the contract when necessary, reject `Idle`, validate the real instance identifier format, and apply a bounded cancellation timeout to execution.

The smoke endpoint is also effectively enabled by default because `GetValue(..., true)` is used. It must be disabled unless an explicit engineering-smoke flag enables it.

### 3. Migration application and readiness behavior are not proven

The evidence contains `dotnet-ef migrations list`, not an explicit migration application. Its raw transcript states that pending/applied status could not be determined. The implementation report nevertheless says the migration was applied.

At application startup, migration exceptions are swallowed and the process continues. `CanConnectAsync` can succeed even when the required schema was not migrated. This permits a false-ready runtime.

Run an explicit `dotnet tool run dotnet-ef database update` against a task-owned PostgreSQL database and retain its result. Startup migration, when enabled, must fail startup or readiness visibly on failure. Readiness must perform a real operation that proves the reviewed schema is usable, such as querying/inserting the engineering probe table without introducing business data.

### 4. Container inputs and Compose identity are not reproducible or isolated

The Dockerfile uses mutable `mcr.microsoft.com/dotnet/sdk:10.0`, `aspnet:10.0`, and `docker/dockerfile:1` references. Recording the currently resolved digest in a log does not pin the build input. Use verified exact version tags and preferably immutable digests in the Dockerfile syntax/base references.

Compose declares fixed `container_name` values (`dxos-postgres`, `dxos-api`), fixed default credentials, `restart: unless-stopped`, and a locally generated image ending in `:latest`. Those settings defeat run-ID isolation and can collide with developer resources. Remove fixed container names and restart policies from the task-owned smoke path, use Compose project scoping/labels, assign a deterministic non-`latest` image tag, and define the documented persistence/volume policy explicitly.

### 5. Runtime cleanup can mutate unrelated Docker resources

Aspire cleanup runs:

```text
docker ps -q --filter "name=postgres-"
docker stop ...
docker rm ...
```

That filter is not bound to the current run, AppHost, labels, or captured pre-state. It can stop/remove an unrelated PostgreSQL container. Errors are then ignored. Codex therefore did not run the submitted Aspire smoke or the Runtime/Full profiles that invoke it.

Track exact process IDs and container IDs created by the current run. Validate labels/ownership and compare against pre-run state before cleanup. Never select resources by a generic name prefix. Cleanup failure must remain non-zero.

Compose has the same ownership risk because fixed container names bypass useful project-name isolation.

### 6. The runtime and evidence runners are not actually bounded

`smoke-runtime.ps1` declares `TimeoutSeconds` but does not use it to bound `docker compose up`, PostgreSQL stop/down, or AppHost lifetime. `run-r4-verification.ps1` calls `WaitForExit()` without a timeout for every step. A hung Docker build, AppHost, or child command can run indefinitely.

Use a native wrapper with argument vectors, bounded `WaitForExit(milliseconds)`, complete stream draining, explicit timeout evidence, current-run process-tree cleanup, and a fail-closed result. Preserve the accepted R3 behavior rather than introducing an unbounded second runner.

### 7. Several evidence checks are false-green or non-replayable

- The secret scan declares four patterns but never evaluates them; it checks only private-key headers and GitHub-token format.
- Resource cleanup evidence merely lists current resources; it does not compare exact before/after container, network, volume, and process sets.
- Image inspection is executed inside a helper PowerShell process that does not check native Docker exit codes, so a failed inspect can still exit `0`.
- The matrix says all 29 steps ran, but it contains only 25 rows; steps 11-14 are comments bundled into step 10.
- Matrix commands point to helper scripts that are deleted after execution, reducing replayability.
- Reports claim `Completed`, a GUID, applied migration, bounded execution, no secrets, and zero-orphan proof where the raw/code evidence does not support those claims.

Keep checks executable in the retained runner or generate machine-readable assertions with their exact logic. Report the true number of executed processes versus covered requirements. Regenerate reports only after the final implementation is frozen.

### 8. Direct package usage needs one minimality pass

`Microsoft.EntityFrameworkCore.Design` is referenced by both API and Infrastructure, and `Microsoft.EntityFrameworkCore.Relational` is directly referenced even though it is normally supplied transitively by the provider. Retain only direct packages with a demonstrated consumer and document why the design-time package belongs at each edge. Do not remove a required package merely to reduce count, but do not keep redundant direct references.

## Required Rework Sequence

1. Remove every committed/default database credential and make missing configuration fail explicitly.
2. Make migration execution and schema-backed readiness fail closed and produce truthful application evidence.
3. Return actual Elsa output/correlation, require the true terminal success state, disable the smoke surface by default, and add a real workflow timeout.
4. Pin Docker build inputs, remove fixed container names/restart behavior, and establish run-labeled resource ownership.
5. Rewrite smoke cleanup to use exact current-run IDs/labels and bounded native execution; prove unrelated resource survival.
6. Harden the external verifier's timeout, native-exit, secret, image, and before/after resource assertions.
7. Re-run Compose and Aspire positive paths plus controlled PostgreSQL-negative paths once, with exact cleanup and lock immutability evidence.
8. Correct the command matrix and reports, then return for Codex Re-review 2.

## State Decision

- BR001-R4: `FIX_REQUIRED`
- Beads `open_source-cab.3`: remain `in_progress`
- OpenSpec R4.1-R4.4: remain unchecked
- BR001-R5/business implementation: not started
- Commit/push/publication: not eligible

Codex changed no production file, did not start or delete any Docker resource, and did not execute the unsafe submitted Aspire cleanup path.
