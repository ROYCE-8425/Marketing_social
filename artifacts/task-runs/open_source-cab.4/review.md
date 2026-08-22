# Codex Independent Review: BR001-R5

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-15 (Asia/Bangkok)

The implementation contains real unit, architecture, PostgreSQL, and Elsa test work, and the submitted direct executable runs produced non-zero CTRF counts. However, BR001-R5 cannot pass because the required `.NET 10` `dotnet test` path is not configured for Microsoft.Testing.Platform and independently returns exit code 0 while discovering zero tests. The frozen execution and evidence also violate the single-authoritative Runtime-run contract and are not mechanically complete.

`open_source-cab.4` remains `in_progress`. BR001-R5.1 through BR001-R5.4 remain unchecked. Codex did not commit, push, close Beads, or modify OpenSpec tasks.

## Independent Results

| Check | Exit | Result |
|---|---:|---|
| `dotnet test tests/DXOS.Unit.Tests/DXOS.Unit.Tests.csproj -c Release --no-build --list-tests` | 0 | No tests discovered |
| `dotnet test tests/DXOS.Unit.Tests/DXOS.Unit.Tests.csproj -c Release --no-build --no-restore` | 0 | False-green zero-test result |
| Strict OpenSpec validation | 0 | Change valid |
| `git diff --check` | 0 | Tracked diff hygiene passed |
| Beads `open_source-cab.4` | 0 | `in_progress`; R3 and R4 dependencies closed |
| Submitted CTRF files | N/A | Unit 10/10, architecture 12/12, integration 2/2 |
| Submitted Runtime evidence | 0 | Reports 12 gates and overall PASS |
| `verification-output.sha256` | N/A | All listed hashes match, but only 12 entries exist for a claimed 13-step run |
| Current task-labeled Docker residue | N/A | No task-owned container, network, or volume found |

## Blocking Findings

### 1. The required .NET 10 MTP `dotnet test` path is not enabled

`global.json` uses:

```json
"testingPlatform": {
  "dotnetTest": {
    "enabled": true
  }
}
```

For .NET 10, the supported runner selection is `"test": { "runner": "Microsoft.Testing.Platform" }`. The current setting is ignored by the native `.NET 10` test command. Independent execution proves the consequence: `dotnet test` exits 0 while discovering zero unit tests.

This directly fails BR001-R5.1, whose acceptance and verification require all retained projects to discover meaningful tests through explicit `dotnet test` commands.

Replace the obsolete/unsupported setting with the .NET 10 runner setting and prove all three explicit `dotnet test` commands discover non-zero tests. Retain the fail-closed count validation.

### 2. The Runtime gate bypasses the required `dotnet test` integration

`scripts/run-test-project.ps1` invokes `dotnet run --project ... -- --report-ctrf` rather than `dotnet test`. This proves that each test project executable can run, but it does not prove the accepted `.NET 10` MTP `dotnet test` workflow. It also concealed Finding 1 because the direct executable finds tests while `dotnet test` does not.

Use explicit per-project `dotnet test` commands with the supported MTP arguments and machine-readable results. The Runtime gate must keep rejecting zero, failed, skipped, missing, and malformed results.

### 3. The frozen runner executes the real suites twice

`run-r5-verification.ps1` executes unit, architecture, and integration separately in steps 5-7, then executes `check.ps1 -Profile Runtime` in step 10, which runs the same three suites again. The real PostgreSQL integration suite therefore runs twice in a purported single frozen run.

Make Runtime the single authoritative positive execution of the three suites. Parse and retain its per-suite results. Negative fixtures such as unavailable Docker may remain separate, but must not perform a second successful integration run.

### 4. Test-result destination validation occurs after filesystem mutation

In `scripts/run-test-project.ps1`, a missing destination directory is created before `Assert-SafePathChain` validates it. A hostile or mistaken `ResultPath` can therefore create a directory outside the approved evidence root before the script rejects it. The function also permits any repository descendant rather than requiring a run-owned result directory.

Resolve and validate the complete result path and every existing ancestor before `New-Item` or `Copy-Item`. Require the destination to be a strict descendant of the caller's run-owned evidence directory. Remove silent cleanup catches and prove timeout process-tree cleanup and result-path escape rejection in the contract verifier.

### 5. Several tests do not protect the behavior they claim

- The connection-string precedence tests configure distinct hosts but only assert `Database.ProviderName`; they would still pass if precedence were reversed. Assert the resolved Npgsql connection string or parsed host/database.
- `EngineeringSmokeWorkflowTests` only asserts two public constants. This is a name/value assertion, not a protected behavior regression test. Replace it with behavior or remove it when the integration test already covers workflow execution.
- `ProductionAssemblies_MustNotReferenceElsaSourceProjectsOrOldCheckout` searches compiled assembly names for the filesystem substring `elsa-core`. Compiled references do not encode project paths, so the rule cannot detect an Elsa source `ProjectReference`. Add a deterministic project-file/reference graph rule for source/path boundaries and a deliberately violating fixture that proves sensitivity.

### 6. Integration cleanup is not bounded or independently owned

`DisposeAsync` creates a 30-second `CancellationTokenSource` but never uses its token; `_container.DisposeAsync()` is therefore unbounded. The test does not record the exact created container ID or mechanically prove its labels before cleanup, and the outer runner only searches container names.

Implement bounded, fail-closed teardown for the exact fixture-owned container. Capture its ID and ownership labels, then prove after the authoritative Runtime run that the exact container and any task-owned network/volume are gone while the canonical pre-existing Docker state is unchanged.

### 7. Docker absence and Docker-state evidence are incomplete

The unavailable-Docker step accepts any non-zero process exit. It does not parse a result artifact or assert the expected Docker diagnostic and zero passed tests. The baseline records containers only, while the post-run audit lists containers, networks, and volumes without exact pre/post comparison. The report's claims of zero task-owned network/volume residue and preserved unrelated state are therefore unsupported by the submitted runner.

Record canonical pre/post inventories for containers, networks, and volumes. The negative run must assert the expected Docker-unavailable failure, non-zero exit, and no successful test count.

### 8. Frozen evidence is incomplete and partly hand-authored

The verification report claims 13 steps, but `verification-output.sha256` contains only the first 12 step logs. Step 13 is created after the sidecar is generated. The sidecar also omits the runner, test helper, contract, configuration, test sources, CTRF files, Runtime/Foundation evidence JSON, per-gate outputs, nine lock files, and Docker inventories.

The report generator hard-codes `10/10`, `12/12`, and `2/2` rather than deriving them from the accepted Runtime CTRF artifacts. The durations hard-coded in the current runner do not match the current `verification.md`, which further proves the report is not reproducibly generated from the frozen inputs.

Regenerate one final evidence set after all code is frozen. Derive counts, durations, commands, exits, gate order, Docker comparison, status, and hashes mechanically. Include parser checks, strict UTF-8/control/mojibake/secret/trailing-whitespace hygiene, exact Git status classification, post-run Beads/OpenSpec state, and a complete sidecar whose own generation order cannot omit the final transcript or step.

## Narrow Rework Sequence

1. Correct `global.json` to the supported .NET 10 MTP runner schema.
2. Change the per-project gate to explicit `dotnet test` and prove non-zero discovery for all three projects.
3. Strengthen the superficial unit and source-boundary architecture tests.
4. Harden result-path validation and bounded Testcontainers teardown/ownership evidence.
5. Change the frozen runner so Runtime is the only successful execution of the real suites.
6. Add exact Docker pre/post comparison and a semantic unavailable-Docker negative result.
7. Freeze code, execute one clean final run, and mechanically generate complete reports and sidecars without hard-coded counts.
8. Return for Codex Re-review 2. Do not commit, push, close Beads, or check R5 tasks before PASS.

## State Decision

- BR001-R5: `FIX_REQUIRED`
- Beads `open_source-cab.4`: remain `in_progress`
- OpenSpec BR001-R5.1 through BR001-R5.4: remain unchecked
- R6 and later work: not authorized
- No commit, push, remote mutation, Beads closure, or OpenSpec task update was performed by Codex

---

# Codex Re-review 5

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-16 (Asia/Bangkok)

The meaningful R5 test implementation is now healthy. The Release solution builds without warnings, all three new teardown fixtures pass independently, the submitted Runtime evidence reports 9 unit, 13 architecture, and 5 integration tests passing, and every current checksum and report link validates. The remaining failures are evidence-lifecycle defects: the claimed external finalizer is still internal and unbounded, Step 10 still records success before its work, the run output directory is still shared, and the Git exact-set check partially derives its expected set from the actual directory contents.

`open_source-cab.4` remains `in_progress`; BR001-R5.1 through BR001-R5.4 remain unchecked.

## Independent Results

| Check | Exit / Result |
|---|---|
| `dotnet build DXOS.slnx -c Release --no-restore -warnaserror` | Exit 0; 0 warnings; 0 errors |
| Direct teardown fixture execution | Exit 0; 3 total, 3 passed, 0 failed, 0 skipped |
| Submitted Runtime CTRF | 9 unit, 13 architecture, and 5 integration tests passed |
| `verification-output.sha256` | 93/93 paths exist and match; 93 unique; no generated-path entries |
| `reports.sha256` | 6/6 paths exist and match; 6 unique |
| Strict artifact hygiene | 49 files inspected; zero invalid UTF-8, trailing whitespace, U+FFFD, or configured mojibake defects |
| Markdown link resolution | 10/10 links resolve from `verification.md` |
| Strict OpenSpec validation | Exit 0; R5.1-R5.4 remain unchecked |
| Beads | `open_source-cab.4` remains `in_progress`; no dependency cycles |
| `git diff --check` | Exit 0 |

The new `ContainerTeardownHelper` and its three fixtures are accepted as meaningful progress. Disposal faults are awaited and propagated; stop failure, disposal failure, and disposal timeout behavior are exercised.

## Remaining Blocking Findings

### 1. The claimed external bounded finalizer is neither external nor bounded

The block labeled `EXTERNAL BOUNDED FINALIZER` is ordinary code at the end of `run-r5-verification.ps1`. It is executed in the same process, under the same lock, after report creation. It does not spawn or supervise a child runner and has no independent timeout. No retained wrapper transcript records the child PID, exact native exit code, wall duration, completed-artifact validation, and cleanup result after the child process terminates.

Consequently, the submitted artifacts cannot independently prove that the only frozen child execution reached exit 0 after final validation. Renaming an internal block does not satisfy the external-finalizer requirement from Re-reviews 3 and 4.

### 2. Step 10 still records success before generating its outputs

At the start of Step 10, the script parses the CTRF files, immediately stops the stopwatch, and writes a successful Step 10 log claiming that `verification.md` and both sidecars were generated. Only afterward does it write the report, verify links, generate the two sidecars, and validate hashes. The second `$sw.Stop()` after generation has no effect because the stopwatch is already stopped.

The retained Step 10 duration is therefore 0.007 seconds and excludes nearly all work named by the step. A later generation failure would leave a false successful Step 10 transcript. This is the same defect identified in Re-review 4.

### 3. Frozen evidence still uses a shared directory rather than a GUID-owned run directory

Although `$runId` is generated, output is still written to the fixed `artifacts/task-runs/open_source-cab.4/raw-logs` directory. Startup deletes every file in that shared directory. The exclusive `.run.lock` prevents two copies of this runner from executing together, but it does not isolate direct Runtime/Full invocations or other tooling from the same output directory, and the run ID is not part of the retained evidence path.

The reparse-point ancestor validation and observable lock removal are improvements, but they do not replace the required GUID-owned strict-descendant directory and promote-after-validation lifecycle.

### 4. The exact Git-status comparison trusts current artifact contents

The implementation now compares two sorted sets, but part of the expected set is constructed by enumerating every file already present in `raw-logs`. An arbitrary extra file placed there before Step 8 is therefore copied into the expected set and accepted rather than reported as unexpected.

The expected set also explicitly requires `review.md`, which is Codex-owned and must not be an implementation prerequisite, plus the transient `.run.lock`. Build the expected artifact inventory from a frozen declarative manifest or from exact step outputs, excluding `review.md`, and then compare both set differences without deriving expectations from the directory being audited.

## Narrow Rework Sequence

1. Add a separate bounded wrapper/finalizer script. It must launch the frozen child exactly once, capture PID/exit/duration/stdout/stderr, wait for termination, then validate final hygiene, links, sidecars, cleanup, and state.
2. Move each child run into `raw-logs/<run-guid>/` or an equivalent GUID-owned strict-descendant directory. Promote exactly one validated run without deleting or mixing another run's evidence.
3. Record Step 10 success and duration only after its report/link/sidecar work succeeds, or move final report ownership entirely to the external wrapper.
4. Replace directory-derived Git expectations with an exact frozen manifest; exclude `review.md` and other Codex-owned state.
5. Freeze the implementation, execute one child through the new wrapper, retain the wrapper transcript/hash, and return for Re-review 6.

## State Decision

- BR001-R5: `FIX_REQUIRED`
- Beads `open_source-cab.4`: remain `in_progress`
- OpenSpec BR001-R5.1 through BR001-R5.4: remain unchecked
- R6 and later work: not authorized
- No commit, push, remote mutation, Beads closure, or OpenSpec task update was performed by Codex

---

# Codex Re-review 3

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-15 (Asia/Bangkok)

The exact Docker inventory comparison, negative Docker CTRF assertions, and current sidecar hashes are meaningful progress. Codex mechanically validated all 162 entries in `verification-output.sha256` and all 6 entries in `reports.sha256`: every listed file exists, every hash matches, and there are no duplicate, malformed, or escaping paths. The remaining blockers are narrower but still prevent a clean-clone-verifiable PASS because the hygiene gate is demonstrably false-green, the primary sidecar binds ignored build intermediates, and the Runtime result/state lifecycle is not actually run-owned or fail-closed.

`open_source-cab.4` remains `in_progress`; BR001-R5.1 through BR001-R5.4 remain unchecked.

## Independently Confirmed Progress

| Check | Exit | Result |
|---|---:|---|
| `verification-output.sha256` | 0 | 162 entries, 162 unique, 0 malformed, missing, escaping, or mismatched entries |
| `reports.sha256` | 0 | 6 entries, 6 unique, 0 malformed, missing, escaping, or mismatched entries |
| Strict OpenSpec validation | 0 | Change valid |
| `git diff --check` | 0 | Tracked diff clean |
| Beads `open_source-cab.4` | 0 | `in_progress`; no dependency cycles |
| Frozen Docker diff JSON | N/A | Recorded pre/post container, network, and volume sets are identical |
| Frozen negative CTRF | N/A | 2 total, 0 passed, 2 failed, 0 skipped |
| Current matching R5 process check | N/A | No runner/test process active apart from Codex's query process |

Also confirmed:

- Runtime now copies explicit fixed CTRF outputs rather than selecting the newest report from each project directory.
- The runner records baseline/post Docker inventories and compares sorted sets.
- Task-labeled Docker container/network/volume queries are enforced before and after the run.
- The unavailable-Docker negative path now parses CTRF counts.
- The current generated `verification.md` is ASCII-safe.

## Remaining Blocking Findings

### 1. The strict text-hygiene gate is false-green

`text-hygiene-report.json` claims zero violations, but independent strict scanning finds:

- literal mojibake sequences `â€”` and `â€“` in `run-r5-verification.ps1`;
- trailing whitespace in `Runtime-runtime-smoke-aspire.out.txt` on lines 23-30 and 32-34;
- trailing whitespace in `Runtime-runtime-smoke-compose.out.txt` on lines 9-13 and 25-27;
- trailing whitespace in the unavailable-Docker step log on line 61.

The scanner misses the mojibake because it checks different, already-double-encoded patterns. Its multiline trailing-whitespace regex also misses spaces before `\r\n`. It does not use a throwing UTF-8 decoder and does not implement the promised control-character audit. Furthermore, step 8 runs before step 9/10 artifacts and final reports/sidecars are generated, so it cannot validate the final evidence set.

Use strict UTF-8 decoding (`throwOnInvalidBytes = true`), line-based CRLF/LF whitespace checks, the actual mojibake patterns, and an explicit forbidden-control-character policy. Run this audit externally after every final artifact is written, or add a final non-self-referential wrapper check. Regenerate evidence only after the scan passes mechanically.

### 2. The primary sidecar is not clean-clone-verifiable

Although all 162 hashes match the current machine, 72 entries point into `src/**/obj/` or `tests/**/obj/`. All 72 are Git-ignored generated intermediates. They will not exist in a clean clone and are not stable implementation inputs. The cause is the recursive source enumeration including `obj` directories.

Exclude `bin`, `obj`, `TestResults`, and other generated/ignored paths from material implementation inputs. Bind only repository deliverables plus explicitly retained run-owned evidence. Add a mechanical assertion that every implementation/input entry is tracked or an explicitly approved task-run artifact, and that no sidecar path is ignored unexpectedly.

### 3. Runtime CTRF outputs are still shared, not run-owned

The contract writes fixed files directly under the shared `artifacts/quality-gate/` root. Its test-gate arguments do not pass `-EvidenceRoot`, so the helper uses the broad default root. The R5 runner has no duplicate-run lock and deletes/reuses shared evidence locations. A concurrent Runtime or Full invocation can overwrite the three CTRF files between gate completion and the R5 runner's copy.

Create a unique Runtime run directory and pass it through `check.ps1` to the test gates. Each gate must receive the exact `-EvidenceRoot` and `-ResultPath` for that run. Record those paths/hashes in Runtime evidence and parse only them. Add a duplicate/concurrency fixture proving one run cannot consume another run's result.

### 4. Testcontainers teardown still suppresses real failures

The disposal wait is now time-bounded, but if `DisposeAsync()` completes in a faulted state the code does not await `disposeTask` after `Task.WhenAny`; its exception is therefore unobserved and the test may pass. `StopAsync` exceptions are also caught and logged without being rethrown. The final Docker audit may catch residue during this frozen runner, but direct required integration execution can still report green after a cleanup failure.

After confirming `disposeTask` won the race, explicitly `await disposeTask` to propagate faults. Preserve the primary test exception if one exists while still surfacing cleanup failure. Add deterministic fixtures for stop failure, disposal fault, and disposal timeout, and ensure required integration execution exits non-zero.

### 5. The final Git/Beads/OpenSpec audit is not fail-closed or exact

Step 9 captures Git status but never compares it against an allowed file-level set. Any extra repository drift would still pass. Its Beads check only fails when a successful text command contains the word `closed`; a failed Beads command passes silently, and `open`, `blocked`, or another status also passes. It does not parse JSON or require the exact issue ID/status. OpenSpec is validated before the run, while the post-run check only searches the four checkbox strings.

Require exit code 0 and parse `bd show open_source-cab.4 --json`, asserting exact ID and `status == in_progress`. Compare `git status --short --untracked-files=all` exactly against an approved file list. Run strict OpenSpec validation again after all state-affecting steps and assert only R5.1-R5.4 remain unchecked as intended.

### 6. Final report-generation evidence remains self-incomplete

Step 10 creates its log and captures its duration before writing `verification.md` and both sidecars. The step log therefore claims generation succeeded before generation occurred, and its published duration excludes most of the step. The final artifacts are also created after the hygiene audit. No external transcript records the runner's final exit and validates the completed sidecars/reports after process termination.

Use an external bounded wrapper: freeze the implementation, run the child once, capture its exact exit/output/duration, then validate final hygiene and both sidecars. The wrapper may produce the final transcript and checksum without editing frozen implementation inputs. Reports must derive from already-finished child evidence and must not claim checks that occurred before their files existed.

### 7. The runner cleanup boundary is still unsafe under concurrency

At startup, the runner deletes every file in `raw-logs` without a run lock, without a per-run directory, and without reparse-point validation of the full path chain. A second runner or another tool using the same task directory can have its evidence removed or mixed into the current run.

Use one GUID-named strict-descendant directory per run, reject reparse points before any deletion/write, and retain or promote exactly one accepted frozen directory only after the child exits and all final validations pass.

## Narrow Rework Sequence

1. Replace the hygiene implementation with a strict final-artifact scanner and fix every independently identified defect.
2. Remove all 72 ignored `obj` entries and reject unexpected ignored/generated paths in the sidecar manifest.
3. Route Runtime CTRF/evidence into one GUID-owned directory and add concurrency isolation assertions.
4. Propagate stop/dispose faults and add teardown failure/timeout fixtures.
5. Make Git, Beads, and post-run OpenSpec checks exact and fail closed.
6. Move final generation, hygiene, and sidecar validation into a bounded external wrapper with truthful timing/exit evidence.
7. Run exactly one new frozen child execution after files are frozen; do not patch its artifacts afterward.
8. Return for Codex Re-review 4. Do not commit, push, close Beads, or check R5 tasks before PASS.

## State Decision

- BR001-R5: `FIX_REQUIRED`
- Beads `open_source-cab.4`: remain `in_progress`
- OpenSpec BR001-R5.1 through BR001-R5.4: remain unchecked
- R6 and later work: not authorized
- No commit, push, remote mutation, Beads closure, or OpenSpec task update was performed by Codex

---

# Codex Re-review 2

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-15 (Asia/Bangkok)

The .NET 10 MTP configuration and the meaningful unit/architecture implementation are now working. Codex independently confirmed 9 unit tests and 13 architecture tests through native `dotnet test`. The submitted Runtime evidence also contains 2 successful integration tests. The remaining defects are narrower and primarily concern cleanup safety, exact Docker-state proof, run-to-result binding, and truthful complete evidence.

`open_source-cab.4` remains `in_progress`; BR001-R5.1 through BR001-R5.4 remain unchecked.

## Independently Confirmed Progress

| Check | Exit | Result |
|---|---:|---|
| Native unit `dotnet test` | 0 | 9 total, 9 passed, 0 failed, 0 skipped |
| Native architecture `dotnet test` | 0 | 13 total, 13 passed, 0 failed, 0 skipped |
| Submitted integration CTRF | N/A | 2 total, 2 passed, 0 failed, 0 skipped |
| Strict OpenSpec validation | 0 | Change valid |
| `git diff --check` | 0 | Tracked diff clean |
| Beads `open_source-cab.4` | 0 | Still `in_progress`; dependency graph has no cycles |
| Verification-output sidecar | N/A | 31/31 listed entries match current files |
| Report sidecar | N/A | 6/6 listed entries match current files |
| Current Docker label query | N/A | No `dxos.task=open_source-cab.4` container, network, or volume residue |

Also confirmed:

- `global.json` now selects `Microsoft.Testing.Platform` through the supported .NET 10 schema.
- Runtime invokes the three retained projects through explicit `dotnet test` calls.
- Connection-string precedence assertions now inspect the resolved connection string.
- The constants-only workflow unit test is gone.
- The architecture suite now parses production project files and includes a deliberate violating `ProjectReference` fixture.
- The positive unit/architecture/integration suites execute only through the single authoritative Runtime profile in the frozen runner.

## Remaining Blocking Findings

### 1. The claimed exact Docker-state comparison is still not implemented

The runner records pre-run containers, networks, and volumes, but step 7 only compares the pre/post **container counts**. It does not compare container IDs, names, images, or states as sets. Networks and volumes are listed after the run but never compared with their pre-run inventories. It also does not query task ownership labels for networks or volumes.

Consequently, a pre-existing container could disappear while an unrelated container appears and the count-only check would pass. Network or volume mutations would also pass. The report's claims of `100% preserved`, `0 mutations`, and exact pre/post comparison are unsupported.

Canonicalize and compare the full pre/post sets for containers, networks, and volumes. Separately query exact task labels and require zero task-owned residue. Preserve these inventories and the mechanical comparison result as run-owned JSON/text evidence.

### 2. Testcontainers teardown and ownership proof remain incomplete

`StopAsync(cts.Token)` is now bounded, but the `finally` block calls `_container.DisposeAsync()` without a bound. If stop times out or disposal hangs, fixture teardown is still unbounded. The test prints the container ID and intended labels, but neither the test nor runner inspects the created Docker object to prove those labels belong to that exact ID before cleanup.

Make the complete teardown path bounded and fail closed. Capture the exact container ID, inspect its real labels, and bind that evidence to the run. After Runtime, prove that exact ID and all resources bearing the run/task labels are absent.

### 3. `ResultPath` is not restricted to the caller's run-owned directory

Validation now occurs before directory creation, which fixes the original ordering defect. However, `scripts/run-test-project.ps1` permits any strict descendant of the repository-wide `artifacts/quality-gate` or `artifacts/task-runs` trees. A gate can therefore overwrite evidence belonging to another task or run.

Pass an explicit approved run-output root and require `ResultPath` to be a strict descendant of that exact directory. Add verifier fixtures proving rejection of sibling task-run paths, sibling quality-gate runs, rooted paths, traversal, and reparse-point ancestors. Timeout cleanup still contains silent catches; make cleanup failure observable and non-zero.

### 4. CTRF results are not mechanically bound to the authoritative Runtime invocation

Step 9 searches each project's `bin/Release/net10.0/TestResults` directory and selects the newest `ctrf-report-*.json`. It does not prove that the selected report was created by step 6, after the current run start, or by the corresponding Runtime gate. A stale/newer unrelated report can be copied and summarized.

Give Runtime a run-owned result path for every suite and record those paths in its evidence. Parse only those exact files. Assert the Runtime evidence schema, overall PASS, exact 12-gate order, gate exits, per-gate output hashes, and the exact linkage between each test gate and CTRF artifact.

### 5. The frozen matrix still omits required final checks

The nine-step runner does not execute or retain:

- PowerShell parser checks for all changed scripts;
- strict UTF-8, control-character, mojibake, secret, and trailing-whitespace hygiene over tracked and untracked deliverables;
- exact final `git status --short --untracked-files=all` classification;
- post-run OpenSpec task-state and Beads-state confirmation;
- a duplicate-run/process preflight and final process/residue audit.

This omission is already observable: `scripts/run-test-project.ps1:114` contains trailing whitespace, `verification.md` contains literal mojibake (`â€”`), and all three retained Runtime test-output files contain U+FFFD replacement characters in the accented repository path. `git diff --check` cannot detect these defects because the affected deliverables are untracked.

### 6. The sidecars are internally valid but do not bind the final implementation state

All 31 raw-log hashes and all 6 report hashes match their listed files. However, the sidecars omit material frozen inputs and deliverables, including `global.json`, `Directory.Packages.props`, `scripts/run-test-project.ps1`, `scripts/check-contract.json`, `scripts/verify-check-contract.ps1`, test project/source files, all nine lock files, and canonical Docker inventories/comparison artifacts.

Add those files to the final mechanical checksum mapping. `review.md` remains Codex-owned and must not be generated or hashed by the implementation runner.

### 7. The unavailable-Docker assertion is not mechanically tied to test counts

The retained negative transcript happens to show 2 total, 2 failed, and 0 succeeded, which is the desired result. The runner itself only checks for a non-zero exit plus any one of several broad words such as `Docker`, `connection`, or `Endpoint`; it does not generate or parse a negative CTRF artifact. The report therefore claims zero false-positive passes without the runner mechanically enforcing that condition.

Generate a run-owned negative result file and assert total > 0, passed = 0, failed > 0, skipped = 0, the expected Docker-unavailable diagnostic, and a non-zero native exit.

### 8. The generated report overstates what was verified

`verification.md` says Docker state was exactly preserved and all sidecars are complete, but Findings 1 and 6 disprove those statements. Its header also contains actual mojibake. Step 9 records its duration and writes a log saying the report/sidecars were generated before those files are actually written, so the published step duration does not cover the reported work.

Generate the step record only after report and sidecar production/validation, or use an external wrapper to capture the final generation outcome without a self-hash cycle. Remove unsupported claims and verify all published links, hashes, counts, and durations mechanically.

## Narrow Rework Sequence

1. Implement exact container/network/volume set comparison and label-based ownership checks.
2. Bound the complete Testcontainers teardown and bind the exact inspected container ID/labels to the run.
3. Restrict `ResultPath` to the explicit current run directory and add sibling/escape/reparse negative fixtures.
4. Pass exact run-owned CTRF destinations through Runtime and eliminate `latest file` discovery.
5. Add parser, strict text hygiene, exact Git status, post-run Beads/OpenSpec, duplicate-process, and final residue checks.
6. Expand sidecars to bind every material input, source, lock, result, Docker inventory, and runner/report artifact.
7. Strengthen the unavailable-Docker negative CTRF assertion.
8. Freeze files, run the matrix exactly once, generate truthful evidence externally or in a non-self-referential order, and return for Re-review 3.

## State Decision

- BR001-R5: `FIX_REQUIRED`
- Beads `open_source-cab.4`: remain `in_progress`
- OpenSpec BR001-R5.1 through BR001-R5.4: remain unchecked
- R6 and later work: not authorized
- No commit, push, remote mutation, Beads closure, or OpenSpec task update was performed by Codex

---

## Latest Review Pointer

The current verdict is **Codex Re-review 3: `FIX_REQUIRED`**. Its detailed findings appear above the historical Re-review 2 section because the review file is append-protected around prior evidence. The authoritative remaining rework is the Re-review 3 narrow sequence covering final hygiene, ignored `obj` sidecar entries, run-owned CTRF isolation, teardown fault propagation, exact state checks, and external final validation.

---

# Codex Re-review 4

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-15 (Asia/Bangkok)

The R5 test foundation and most of its evidence are now materially healthy. Codex mechanically verified both checksum manifests, strict text hygiene, and the linkage from the Runtime gates to the three current-run CTRF files. The remaining blockers are limited to final evidence lifecycle, run isolation, exact state classification, and missing deterministic teardown-failure coverage.

`open_source-cab.4` remains `in_progress`; BR001-R5.1 through BR001-R5.4 remain unchecked.

## Independently Confirmed Progress

| Check | Result |
|---|---|
| `verification-output.sha256` | 91/91 entries exist and match; 91 unique paths; no `bin`, `obj`, ignored, malformed, or escaping entries |
| `reports.sha256` | 6/6 entries exist and match; 6 unique paths |
| Strict text scan | 57 inspected evidence/source files; zero invalid UTF-8, trailing whitespace, U+FFFD, or configured mojibake patterns |
| Runtime CTRF linkage | Unit, architecture, and integration gates reference existing CTRF files under the submitted `raw-logs` directory with matching hashes |
| Test result evidence | 9 unit, 13 architecture, and 2 integration tests passed; negative Docker CTRF records 2/2 failures |
| OpenSpec | Strict validation passes; R5.1-R5.4 remain unchecked |
| Beads | `open_source-cab.4` is `in_progress`; no dependency cycles |
| Git hygiene | `git diff --check` passes |

Also confirmed:

- The sidecar no longer contains generated `bin` or `obj` paths.
- `{EvidenceDir}` is expanded from the supplied evidence-file directory and is recorded in Runtime expected-output checks.
- `DisposeAsync()` is now awaited, and a captured `StopAsync()` exception is rethrown.
- The submitted frozen evidence contains exact pre/post Docker inventories and a comparison artifact.

## Remaining Blocking Findings

### 1. Step 10 still reports success before doing its work

`run-r5-verification.ps1` stops the Step 10 stopwatch and writes the successful Step 10 log before generating `verification.md`, `verification-output.sha256`, and `reports.sha256`. The published Step 10 duration is consequently only 0.006 seconds and excludes the work named by the step. If report or sidecar generation fails afterward, the retained Step 10 transcript still falsely says it succeeded.

There is no bounded external wrapper transcript recording the child runner's final exit code, duration, completed-artifact hygiene result, and post-process checksum validation. This is the same explicit finalization requirement from Re-review 3 and remains unsatisfied.

### 2. The evidence directory is locked but is not uniquely run-owned

The runner still reuses `artifacts/task-runs/open_source-cab.4/raw-logs` and deletes every file directly inside it at startup. The new `.run.lock` prevents a second copy of this particular runner from using that task directory concurrently, but it does not create the required GUID-owned strict-descendant run directory and does not prevent another direct Runtime invocation from writing to the same destination.

The path check is a string-prefix check only. It does not reject reparse points in the evidence/raw-log ancestor chain before deletion. Lock-file removal also uses `-ErrorAction SilentlyContinue`, allowing an exit-0 run to leave a stale lock without reporting cleanup failure.

### 3. Git status validation is an allow-pattern filter, not an exact set comparison

Step 8 proves only that every observed line matches one of several broad patterns or prefixes. It does not require an expected path to be present, compare normalized expected and actual sets, or constrain the task artifact prefix to the exact submitted files. For example, any arbitrary untracked file below `artifacts/task-runs/open_source-cab.4/` is accepted, including evidence not belonging to the frozen run.

Replace the patterns/prefixes with a mechanically derived exact file-level set for the frozen pre-PASS state and compare both set differences. Keep `review.md` explicitly Codex-owned rather than admitting the entire directory.

### 4. Required teardown failure fixtures are still absent

The production fixture now awaits disposal and rethrows a captured stop failure, which fixes the direct implementation defect. However, no deterministic test was added for stop failure, disposal fault, or disposal timeout. A repository search finds only the production teardown path; the contract verifier does not exercise these three required failure modes. The submitted positive integration result therefore does not prove that cleanup faults make required execution non-zero.

### 5. Published report links are not valid relative links

`verification.md` resides in `artifacts/task-runs/open_source-cab.4/`, but its matrix links use paths beginning with `artifacts/task-runs/open_source-cab.4/raw-logs/...`. Markdown resolves those relative to the report directory, producing a duplicated nonexistent path. Use `raw-logs/<file>` links or repository-root absolute links supported by the renderer, and mechanically verify every published link target.

## Narrow Rework Sequence

1. Freeze the implementation and add an external bounded finalizer that invokes the child once, records its final exit/duration/transcript, and validates completed artifacts after child termination.
2. Generate into one GUID-owned strict-descendant directory; validate the full non-reparse ancestor chain before any cleanup/write, and make lock/cleanup failure observable.
3. Move the Step 10 success record after all named generation and validation, or let the external finalizer own the final generation outcome without a self-hash cycle.
4. Compare exact normalized Git-status sets rather than broad allowed patterns/prefixes.
5. Add deterministic stop-failure, dispose-fault, and dispose-timeout fixtures and require their executions to fail closed.
6. Correct and mechanically validate all report links, regenerate one final frozen evidence set, and return for Re-review 5.

## State Decision

- BR001-R5: `FIX_REQUIRED`
- Beads `open_source-cab.4`: remain `in_progress`
- OpenSpec BR001-R5.1 through BR001-R5.4: remain unchecked
- R6 and later work: not authorized
- No commit, push, remote mutation, Beads closure, or OpenSpec task update was performed by Codex

---

## Latest Review Pointer

The authoritative current verdict is **Codex Re-review 5: `FIX_REQUIRED`**. The Re-review 5 section appears above the historical Re-review 3, Re-review 2, and Re-review 4 material in this append-protected review file. Its remaining scope is limited to truthful external finalization, Step 10 ordering, GUID-owned run isolation, and a non-self-derived exact Git artifact set.

---

# Codex Re-review 6

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-16 (Asia/Bangkok)

The four Re-review 5 implementation blockers are substantially resolved: a separate bounded wrapper now supervises one child, the accepted evidence is isolated under a GUID directory, Step 10 records its work after generation, and Git status is compared against declarative inventories. The R5 implementation and current artifacts remain healthy. The finalizer's own evidence lifecycle, however, still records a successful final verdict before its last validations and does not retain the captured child streams. The child runner also deletes a reused run directory before performing its own safety validation.

`open_source-cab.4` remains `in_progress`; BR001-R5.1 through BR001-R5.4 remain unchecked.

## Independently Confirmed Progress

| Check | Result |
|---|---|
| Active matching R5 processes | None; only the inspection command matched its own query text |
| Concurrency lock | `.run.lock` absent after completion |
| Frozen run directory | Exactly `runs/8c46c86017b1/` retained |
| Child execution evidence | PID 26844; exit 0; 127.032 seconds recorded |
| Wrapper evidence | PID 25640; 129.854 seconds recorded |
| Step 10 | Exit 0; truthful retained duration 0.159 seconds; log written after report/sidecar generation |
| `verification-output.sha256` | 95/95 entries exist and match; 95 unique paths; zero generated-path entries |
| `reports.sha256` | 7/7 entries exist and match; 7 unique paths |
| Strict current artifact scan | 52 files inspected independently; zero invalid UTF-8, trailing whitespace, U+FFFD, or configured mojibake defects |
| Markdown links | Current report links resolve into the GUID run directory |
| Git hygiene | `git diff --check` exits 0 |
| OpenSpec | Strict validation exits 0; R5.1-R5.4 remain unchecked |
| Beads | `open_source-cab.4` remains `in_progress`; no dependency cycles |

The accented repository path displayed incorrectly through Windows PowerShell `Get-Content` because of its default decoding. Strict UTF-8 byte decoding of the retained file produces the correct text, so Codex does not classify that display as an artifact encoding defect.

## Remaining Blocking Findings

### 1. The finalizer transcript declares final success before finalization completes

`run-r5-final-verification.ps1` writes `finalizer-transcript.log` with `FINALIZER VERDICT: SUCCESS (Exit 0)` at lines 336-367. Only afterward does it:

- append the transcript and transcript checksum to `verification-output.sha256`;
- update `reports.sha256`;
- revalidate both updated sidecars;
- execute the final exact Git-status comparison; and
- reach the wrapper's actual exit 0.

If any of those later operations fails, the retained transcript still falsely declares finalizer success. The transcript also claims only 93 verification outputs because it is written before the final two entries are added; the current authoritative manifest contains 95.

Write the success transcript only after every validation succeeds. Avoid the self-hash cycle by separating a pre-verdict execution record from a final externally bound completion record, or by having an outermost minimal supervisor bind the completed wrapper result.

### 2. Captured child stdout and stderr are not retained

The wrapper correctly captures `$childStdout` and `$childStderr`, but it only sends stdout to `Write-Host` and prints stderr on failure. Neither stream is retained in the GUID run directory or referenced by a checksum manifest. `finalizer-transcript.log` contains only a summary, not the captured child streams requested by the Re-review 5 contract.

Persist exact child stdout and stderr as separate run-owned files before final validation, include their sizes and SHA-256 hashes in the final record, and bind both files in the declarative inventory and sidecar.

### 3. The child runner validates its run directory after deleting from it

In `run-r5-verification.ps1`, an existing `runs/<RunGuid>` directory is enumerated and its direct files are deleted before `Assert-SafePathChain` validates that run directory. The child also accepts any string as `RunGuid` and silently reuses an existing directory. The external wrapper validates the current submitted path first, so the accepted frozen run was not harmed, but the child script itself remains unsafe when invoked directly and permits replacement of older run evidence.

Require the exact run-ID format, validate the complete path/reparse chain before any deletion or write, and fail if the GUID directory already exists. A frozen run ID must be single-use; do not erase and reuse prior evidence.

## Narrow Rework Sequence

1. Persist exact child stdout/stderr in the GUID directory and add them to the declarative inventory and checksum evidence.
2. Move the final SUCCESS record after updated-sidecar validation, final exact Git comparison, lock/residue checks, and every other fallible operation. Ensure the recorded final counts match the completed manifests.
3. Validate `RunGuid` and its path before mutation; reject any pre-existing run directory instead of deleting/reusing it.
4. Freeze inputs and execute exactly one new wrapper/child run. If it fails, stop without editing or rerunning.
5. Return for Re-review 7 without modifying `review.md`, closing Beads, checking OpenSpec tasks, committing, pushing, or starting R6.

## State Decision

- BR001-R5: `FIX_REQUIRED`
- Beads `open_source-cab.4`: remain `in_progress`
- OpenSpec BR001-R5.1 through BR001-R5.4: remain unchecked
- R6 and later work: not authorized
- No commit, push, remote mutation, Beads closure, or OpenSpec task update was performed by Codex

---

## Latest Review Pointer

The authoritative current verdict is **Codex Re-review 6: `FIX_REQUIRED`**. Only the finalizer completion record, retained child streams, and child run-directory pre-mutation validation remain blocking; the R5 test implementation itself is accepted as healthy.

---

# Codex Re-review 7

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-16 (Asia/Bangkok)

All three Re-review 6 implementation defects are now corrected: exact child streams are retained and hashed, both wrapper and child validate a single-use hexadecimal run ID before mutation, and exactly one new GUID-owned run remains. The current code, tests, sidecars, hygiene, Git state, OpenSpec state, and Beads state are healthy. One final evidence-ordering defect remains: the execution transcript and wrapper duration are finalized before the sidecars and exact Git status are generated and validated, while the transcript claims that work already completed.

`open_source-cab.4` remains `in_progress`; BR001-R5.1 through BR001-R5.4 remain unchecked.

## Independently Confirmed Progress

| Check | Result |
|---|---|
| Active R5 wrapper/child processes | None |
| Concurrency lock | `.run.lock` absent after completion |
| Frozen runs | Exactly one directory: `runs/e9bd2c8056f7/` |
| Run-ID safety | Wrapper and child require 8-32 hexadecimal characters and reject an existing run directory before mutation |
| Retained child stdout | 1,299 bytes; SHA-256 matches `50d26bc36c87d797e6d9a7b8f8b5c81fe45343831203afe0dc1ee8502c627b15` |
| Retained child stderr | 0 bytes; SHA-256 matches the SHA-256 empty-file digest |
| Child execution | PID 15964; exit 0; 125.892 seconds recorded |
| Step 10 | Exit 0; 0.291 seconds; retained log written after its report/sidecar work |
| `verification-output.sha256` | 97/97 entries exist and match; 97 unique; no generated-path entries |
| `reports.sha256` | 7/7 entries exist and match; 7 unique |
| Strict current artifact scan | 54 task-run files inspected independently; zero invalid UTF-8, trailing whitespace, U+FFFD, or configured mojibake defects |
| Git hygiene | `git diff --check` exits 0 |
| OpenSpec | Strict validation exits 0; R5.1-R5.4 remain unchecked |
| Beads | `open_source-cab.4` remains `in_progress`; no dependency cycles |

The submitted Runtime evidence continues to report 9 unit, 13 architecture, and 5 integration tests passing, with E2E explicitly `NOT_APPLICABLE`.

## Remaining Blocking Finding

### The final execution record is written before the work it claims is complete

`run-r5-final-verification.ps1` captures `$wrapperEndTime` and `$wrapperDurationSeconds`, then writes `finalizer-transcript.log` at lines 314-352. The transcript states:

- `Final Sidecar Manifests: Total 97 verification outputs and 7 reports bound`; and
- `EXECUTION RECORD STATUS: CHILD_PASS_AND_VALIDATIONS_VERIFIED`.

Only after that transcript is written does the wrapper generate the final 97-entry `verification-output.sha256`, generate the 7-entry `reports.sha256`, validate both manifests, perform the final exact Git-status comparison, print the wrapper success banner, and reach exit 0.

Thus the recorded wrapper end time and 129.549-second duration exclude the final manifest and Git work. If any operation after line 352 fails, the retained transcript still claims the final sidecars were bound and validations verified. The wrapper's hygiene scan also occurs before the transcript and transcript checksum exist, so its own final artifacts are not part of the reported 108-file scan.

## Final Narrow Fix

1. Do not claim final sidecar binding, final Git validation, wrapper completion, or a final wrapper duration in any record written before those operations.
2. Use a non-self-referential completion design. For example:
   - write a pre-finalization child/validation record without final sidecar or wrapper-success claims;
   - generate and validate manifests and exact Git state;
   - write a minimal completion record with the true end time/duration and exact completed counts;
   - hash that completion record in a separate checksum file whose validation does not require rewriting the completed record or manifests.
3. Strictly scan every newly written finalizer record after it is created.
4. Freeze inputs and execute exactly one new wrapper/child run. Stop on the first failure and do not patch or rerun.
5. Return for Re-review 8 without modifying `review.md`, closing Beads, checking OpenSpec tasks, committing, pushing, or starting R6.

## State Decision

- BR001-R5: `FIX_REQUIRED`
- Beads `open_source-cab.4`: remain `in_progress`
- OpenSpec BR001-R5.1 through BR001-R5.4: remain unchecked
- R6 and later work: not authorized
- No commit, push, remote mutation, Beads closure, or OpenSpec task update was performed by Codex

---

## Latest Review Pointer

The authoritative current verdict is **Codex Re-review 7: `FIX_REQUIRED`**. Only truthful post-manifest completion ordering remains blocking; the R5 implementation and all current artifacts validate successfully.

---

# Codex Re-review 8

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-16 (Asia/Bangkok)

The R5 implementation, tests, retained streams, manifests, text hygiene, Docker cleanup evidence, OpenSpec validation, and Beads state remain healthy. The split child/completion records remove the previous self-reference problem. One narrow ordering defect still prevents the completion record from being truthful: it declares finalizer success and freezes the wrapper end time before the final exact Git-status assertion runs.

`open_source-cab.4` remains `in_progress`; BR001-R5.1 through BR001-R5.4 remain unchecked.

## Independently Confirmed Results

| Check | Result |
|---|---|
| Frozen runs | Exactly one retained run: `runs/932cea16c725/` |
| Concurrency lock | `.run.lock` absent after the run |
| Child execution record | Exit 0; retained stdout/stderr sizes and hashes match the record |
| Completion checksum | `finalizer-completion.sha256` matches `finalizer-completion.log` |
| `verification-output.sha256` | 96/96 unique entries exist and match; no `bin`, `obj`, or `TestResults` paths |
| `reports.sha256` | 7/7 unique entries exist and match |
| Strict task-run hygiene | 55 files inspected; zero invalid UTF-8, trailing whitespace, U+FFFD, or configured mojibake defects |
| Verification report links | 10/10 relative links resolve to existing files |
| Git hygiene | `git diff --check` exits 0 |
| OpenSpec | Strict validation exits 0; R5.1-R5.4 remain unchecked |
| Beads | `open_source-cab.4` is `in_progress`; no dependency cycles |

The retained Runtime evidence continues to report 9 unit, 13 architecture, and 5 integration tests passing, with E2E explicitly `NOT_APPLICABLE`. Codex did not rerun the full test matrix.

## Remaining Blocking Finding

### The completion record still precedes a fallible final assertion

In `run-r5-final-verification.ps1`:

- lines 483-484 capture `$wrapperEndTime` and calculate the claimed full wrapper duration;
- lines 486-519 write `finalizer-completion.log`, declare `FINALIZER_COMPLETED_SUCCESSFULLY (Exit 0)`, and create its checksum;
- lines 521-525 perform post-creation hygiene checks that may still throw;
- lines 528-645 then execute the final exact Git-status set comparison, which may still throw;
- only line 650 actually exits 0.

Therefore the recorded 118.004-second wrapper duration excludes completion-record creation, checksum creation, their hygiene checks, and the final Git assertion. More importantly, a Git-set mismatch at line 642 would leave a retained record claiming successful exit 0 even though the wrapper subsequently failed.

The completion record also claims the concurrency lock was acquired and released, but it is written while the wrapper still holds the lock; release occurs later through the outer cleanup lifecycle. That claim must describe an already completed event, not an intended one.

## Final Narrow Fix

1. Keep `child-execution.log` as the immutable pre-finalization record.
2. Complete every fallible assertion first, including exact Git-state validation and post-created-file hygiene.
3. Do not claim that the lock has been released while execution still owns it.
4. Emit the success/completion record from a non-self-referential terminal mechanism after the wrapper has established the final outcome. Its timing must cover all work it claims to measure.
5. Add a mechanical negative fixture proving that a deliberately induced final Git-set mismatch cannot leave a success completion record.
6. This is an evidence-wrapper-only correction. Do not modify production/test behavior or rerun the expensive child matrix merely to edit the wrapper. First validate the ordering with a small fixture; perform one new frozen full run only after the wrapper is frozen.
7. Return for Re-review 9 without modifying `review.md`, closing Beads, checking OpenSpec tasks, committing, pushing, or starting R6.

## State Decision

- BR001-R5: `FIX_REQUIRED`
- Beads `open_source-cab.4`: remain `in_progress`
- OpenSpec BR001-R5.1 through BR001-R5.4: remain unchecked
- R6 and later work: not authorized
- No commit, push, remote mutation, Beads closure, or OpenSpec task update was performed by Codex

---

## Latest Review Pointer

The authoritative current verdict is **Codex Re-review 8: `FIX_REQUIRED`**. Only terminal completion-record ordering remains blocking; the R5 implementation and retained test evidence are otherwise accepted as healthy.

---

# Codex Re-review 9

## Verdict

`FIX_REQUIRED`

Review date: 2026-08-16 (Asia/Bangkok)

The pre-completion ordering is materially improved: the lock is released and the 82-entry projected Git set is validated before the completion files are created. The R5 implementation and retained test evidence remain accepted as healthy. Two narrow evidence defects remain: the completion record still freezes a purported wrapper end before post-completion work, and the claimed mechanical ordering fixture is absent from the repository and all checksum manifests.

`open_source-cab.4` remains `in_progress`; BR001-R5.1 through BR001-R5.4 remain unchecked.

## Independently Confirmed Results

| Check | Result |
|---|---|
| Frozen runs | Exactly one retained run: `runs/3fa5b277a2c4/` |
| Concurrency lock | `.run.lock` absent |
| Wrapper syntax | PowerShell parser reports zero errors |
| Completion checksum | Matches `finalizer-completion.log` |
| `verification-output.sha256` | 96/96 unique entries exist and match |
| `reports.sha256` | 7/7 unique entries exist and match |
| Strict task-run hygiene | 55 applicable files inspected; zero invalid UTF-8, trailing whitespace, U+FFFD, or configured mojibake defects |
| Verification links | 10/10 relative links resolve |
| Current Git inventory | 84 expected R5 paths excluding Codex-owned `review.md`; `git diff --check` exits 0 |
| OpenSpec | Strict validation exits 0; R5.1-R5.4 remain unchecked |
| Beads | `open_source-cab.4` is `in_progress`; no dependency cycles |
| Docker residue | Zero containers, networks, or volumes labeled `dxos.task=open_source-cab.4` |

The retained Runtime evidence reports 9 unit, 13 architecture, and 5 integration tests passing, with E2E explicitly `NOT_APPLICABLE`. Codex did not rerun the full test matrix.

## Blocking Findings

### 1. The recorded wrapper end and success still precede post-completion work

In `run-r5-final-verification.ps1`:

- lines 592-593 capture `$wrapperEndTime` and calculate 117.938 seconds;
- lines 595-627 write the record declaring `FINALIZER_COMPLETED_SUCCESSFULLY (Exit 0)` and its checksum;
- lines 629-633 then perform fallible hygiene checks on those new files;
- lines 635-661 then perform the fallible post-completion exact Git comparison;
- only line 666 reaches `exit 0`.

The record is therefore not a true complete wrapper duration and is not emitted after every operation it describes as final. The new `catch` attempts to remove false-positive completion files, which is useful, but both removals use `-ErrorAction SilentlyContinue` and their absence is never asserted. A locked, read-only, permission-denied, or otherwise undeletable record can survive a failed wrapper.

### 2. The claimed negative ordering fixture is not reproducible

The handoff states that `test-finalizer-ordering-fixture.ps1` was implemented and executed. That file does not exist anywhere under `artifacts/task-runs/open_source-cab.4`, no equivalent fixture was found in the retained scripts, and neither checksum manifest names it or a fixture transcript.

Consequently there is no independently repeatable proof for either claimed case:

- a pre-completion Git mismatch prevents the success record; or
- a post-completion failure removes both completion files.

The second case is especially important because the production cleanup currently suppresses deletion errors.

## Final Narrow Fix

1. Retain the ordering fixture and its transcript, and bind both into the appropriate checksum manifest.
2. Make cleanup observable and fail-closed: do not suppress removal errors, and assert both completion paths are absent before propagating failure.
3. Avoid describing `$wrapperEndTime` as the complete wrapper end when post-record work remains. Prefer a terminal atomic publication design: prepare and validate completion material in a run-owned staging location, validate the projected final Git set, then publish the already-validated completion unit as the last state-changing operation.
4. The negative fixture must prove both pre-publication failure and post-publication/cleanup failure behavior without running the expensive test child.
5. Freeze the wrapper only after that small fixture passes. Then execute exactly one new frozen full run and stop regardless of its result.
6. Return for Re-review 10 without modifying `review.md`, closing Beads, checking OpenSpec tasks, committing, pushing, or starting R6.

## State Decision

- BR001-R5: `FIX_REQUIRED`
- Beads `open_source-cab.4`: remain `in_progress`
- OpenSpec BR001-R5.1 through BR001-R5.4: remain unchecked
- R6 and later work: not authorized
- No commit, push, remote mutation, Beads closure, or OpenSpec task update was performed by Codex

---

## Latest Review Pointer

The authoritative current verdict is **Codex Re-review 9: `FIX_REQUIRED`**. The R5 tests and runtime gate are accepted; only reproducible, fail-closed terminal evidence publication remains blocking.

---

# Codex Re-review 10

## Verdict

`PASS`

Review date: 2026-08-16 (Asia/Bangkok)

BR001-R5 now satisfies the meaningful-test-foundation acceptance contract. The retained fixture and the production wrapper share the same atomic publication helper; all four ordering scenarios pass; the single frozen run completes successfully; the completion unit is published atomically as the final repository mutation; and the test, checksum, hygiene, Docker, OpenSpec, and Beads evidence validates independently.

The parser error mentioned after the run came from a separately launched ad-hoc `powershell.exe -Command` inspection task. It did not execute inside the frozen wrapper or child, did not change the retained artifacts, and does not invalidate frozen run `8682f54269a9`.

## Independent Results

| Check | Result |
|---|---|
| Frozen run | Exactly one retained run: `runs/8682f54269a9/` |
| Wrapper/child | Child PID 9428; child exit 0; 114.709 seconds recorded |
| Script parsing | Wrapper, shared publication helper, and ordering fixture parse with zero PowerShell errors |
| Ordering fixture | Four of four scenarios pass in the retained 0.237-second transcript |
| Shared implementation | Fixture and wrapper both invoke `Publish-CompletionUnit` from `finalizer-publish-helper.ps1` |
| Atomic completion | Exactly two files under `runs/8682f54269a9/completion/`; no staging residue |
| Completion checksum | Matches `finalizer-completion.log` |
| Primary sidecar | 96/96 unique entries exist and match |
| Reports sidecar | 10/10 unique entries exist and match, including helper, fixture, and fixture transcript |
| Unit tests | 9 total; 9 passed; 0 failed; 0 skipped |
| Architecture tests | 13 total; 13 passed; 0 failed; 0 skipped |
| Integration tests | 5 total; 5 passed; 0 failed; 0 skipped |
| Docker-negative boundary | 2 tests executed against unavailable Docker; 2 failed as required; no false skip/pass |
| E2E | Correctly recorded as `NOT_APPLICABLE` |
| Report links | 10/10 relative links resolve |
| Strict task-run hygiene | 58 applicable files independently decoded/scanned; zero detected violations |
| Git hygiene | `git diff --check` exits 0 |
| OpenSpec | Strict validation exits 0; R5.1-R5.4 remain unchecked before PASS finalization |
| Beads | `open_source-cab.4` remains `in_progress`; no dependency cycles |
| Docker residue | Zero task-labeled containers, networks, or volumes |

Codex did not rerun the expensive test matrix. Acceptance is based on the single retained run, direct parsing of its CTRF/evidence records, independent checksum validation, static inspection of the wrapper/helper/fixture, and current state checks.

## Acceptance Decision

- BR001-R5.1: accepted
- BR001-R5.2: accepted
- BR001-R5.3: accepted
- BR001-R5.4: accepted
- BR001-R5 overall: `PASS`

## Authorized Post-PASS Finalization

Use a separate, narrow finalization action. It may:

1. mark only OpenSpec BR001-R5.1 through BR001-R5.4 complete;
2. run strict OpenSpec validation;
3. run the Foundation and Runtime quality gates once against the accepted state;
4. verify `git diff --check`, exact Git status, no R5 Docker residue, and no Beads cycles;
5. close only Beads issue `open_source-cab.4` with this PASS as the reason;
6. create one dedicated milestone commit with a concise human-authored message;
7. push normally to the Product Owner-approved DX-OS remote, without force and without rewriting earlier history;
8. verify local HEAD equals the remote branch SHA and the working tree is clean.

Do not combine R6 implementation with the R5 milestone commit. Begin R6 only after the R5 finalization commit and push are independently confirmed.

## State Decision

- BR001-R5: `PASS`
- Current Beads state observed by Codex: `open_source-cab.4` still `in_progress`
- Current OpenSpec state observed by Codex: R5.1-R5.4 still unchecked
- R6: authorized only after the separate R5 post-PASS finalization checkpoint
- Codex performed no OpenSpec checkbox mutation, Beads closure, commit, push, remote mutation, or R6 work

---

## Latest Review Pointer

The authoritative current verdict is **Codex Re-review 10: `PASS`**. BR001-R5 is accepted and ready for a separate milestone finalization commit/push before R6 begins.
