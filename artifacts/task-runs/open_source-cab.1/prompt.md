# Gemini Implementation Contract

```yaml
task_id: open_source-cab.1
openspec_task: BR001-R1
title: Safely extract DX-OS into an independent repository

objective: >-
  Create a new DX-OS-owned repository from a reviewed, hash-verified inventory
  while preserving the current Elsa Core checkout and all DX-OS OpenSpec,
  Beads, source, documentation, and evidence required for later remediation.

business_context: >-
  BOOTSTRAP AUDIT 001 is accepted with verdict NOT_READY. Product work cannot
  start while DX-OS is an untracked overlay on the Elsa Core repository because
  repository identity, reproducibility, review evidence, security claims, and
  competition/demo credibility are not trustworthy. This task creates the safe
  ownership boundary on which every later remediation task depends. It is not a
  business feature and it must not attempt to make the build/runtime READY.

source_of_truth:
  - openspec/changes/bootstrap-remediation-001/proposal.md
  - openspec/changes/bootstrap-remediation-001/design.md, especially Decisions 1 and 2
  - openspec/changes/bootstrap-remediation-001/plan.md, R1 Repository Extraction
  - openspec/changes/bootstrap-remediation-001/tasks.md, BR001-R1.1 through BR001-R1.4
  - openspec/changes/bootstrap-remediation-001/specs/repository-foundation/spec.md
  - openspec/changes/bootstrap-remediation-001/specs/agent-governance/spec.md
  - docs/audits/BOOTSTRAP-AUDIT-001.md
  - DXOS_CODEX_MASTER_HANDOFF.md, sections 23 through 25
  - Beads issue open_source-cab.1 under epic open_source-cab

current_state:
  source_checkout: 'C:\Users\199X\OneDrive\Máy tính\olympic\open_source'
  source_revision_at_prompt_creation: 9bc602ff6fde0eafc3d54662a21bcac7f62760a1
  source_branch: main
  source_remote: 'origin=https://github.com/elsa-workflows/elsa-core.git'
  audit_verdict: NOT_READY
  openspec_validation: 'PASS: bootstrap-remediation-001 strict validation, 1/1'
  beads_ready_task: open_source-cab.1
  facts:
    - The current Git history, origin, root build metadata, and root AGENTS.md belong to Elsa Core.
    - DXOS.slnx lists DX-OS-named projects and no Elsa project, but those projects inherit Elsa root configuration.
    - DX-OS source, tests, scripts, docs, OpenSpec, and Beads state are currently untracked/overlaid in this checkout.
    - Existing bin/obj artifacts and placeholder tests must not be copied as repository truth.
    - The target DX-OS remote URL and source license are not owner-approved yet.

target_state:
  default_target: 'C:\Users\199X\OneDrive\Máy tính\olympic\dx-os'
  repository:
    - Exists in a new, previously absent or explicitly empty directory outside the Elsa checkout.
    - Contains only manifest-reviewed DX-OS-owned artifacts plus deliberately recreated minimal root metadata.
    - Has a new local Git repository/default branch and one local initial extraction commit with no Elsa history.
    - Has no remote unless a Product Owner-approved non-Elsa URL is already provided; otherwise records REMOTE_PENDING_OWNER_AUTHORIZATION.
    - Has a newly initialized Beads database containing the accepted epic, eight children, dependencies, and OpenSpec spec links.
    - Contains no Elsa source tree, Elsa project reference, Elsa build/release infrastructure, inherited NuGet feeds/catalog, generated build output, or Elsa Git metadata.
  preservation:
    - The Elsa checkout remains present and unchanged except for task evidence and supported Beads export/backup operations created by this task.
    - The extraction inventory, hashes, copy decisions, Beads migration, commands, exit codes, and blockers are durable in both repositories.

allowed_scope:
  old_checkout_writes:
    - artifacts/task-runs/open_source-cab.1/inventory.csv
    - artifacts/task-runs/open_source-cab.1/extraction-manifest.json
    - artifacts/task-runs/open_source-cab.1/beads-issues.jsonl
    - artifacts/task-runs/open_source-cab.1/beads-backup/** when supported and safe
    - artifacts/task-runs/open_source-cab.1/implementation-report.md
    - artifacts/task-runs/open_source-cab.1/verification.md
    - supported Beads database metadata required to export/backup or update this issue
  new_repository_candidates_to_review:
    - DXOS_CODEX_MASTER_HANDOFF.md
    - docs/audits/BOOTSTRAP-AUDIT-001.md and other clearly DX-OS-owned docs
    - openspec/config.yaml and openspec/changes/bootstrap-remediation-001/**
    - reviewed .agents/rules/** and only DX-OS-relevant OpenSpec/Beads skills
    - DXOS.slnx
    - src/DXOS.*/** excluding bin, obj, caches, locks, and inherited/generated files
    - tests/DXOS.*/** excluding bin, obj, caches, locks, and placeholder evidence classification
    - reviewed DX-OS scripts and compliance documents, retaining their NOT_READY status
    - artifacts/task-runs/open_source-cab.1/**
  new_repository_recreate_minimally:
    - .gitignore with generated/tool/secret exclusions
    - AGENTS.md containing only DX-OS identity, current OpenSpec/Beads pointers, extraction safety, and no-business-work gate; full governance is BR001-R6
    - README.md or migration note identifying NOT_READY status and next Beads task
    - LICENSE-PENDING.md rather than inventing or copying a source license
  explicitly_defer_to_later_tasks:
    - global.json, Directory.Build.props, Directory.Build.targets, Directory.Packages.props, NuGet.Config, and package metadata to BR001-R2
    - final scripts/check.ps1 behavior to BR001-R3
    - runtime, packages, Compose, Aspire, PostgreSQL, and Elsa smoke to BR001-R4
    - meaningful tests to BR001-R5
    - full agent rules/ADRs/project state to BR001-R6
    - CI/scanners/SBOM/final OSS notices/source license to BR001-R7

forbidden:
  - Do not delete, move, rename, clean, or mass-edit the Elsa checkout.
  - Do not rewrite/filter Elsa history, alter its branch/remotes, force push, or push to elsa-workflows/elsa-core.
  - Do not use mass deletion as extraction or make the old checkout the new DX-OS repository.
  - Do not overwrite an existing target directory, follow an unresolved path, or write when target safety checks fail.
  - Do not copy .git, live .beads locks/embedded database files, bin, obj, TestResults, logs, caches, tool binaries, secrets, or local credentials.
  - Do not blindly copy Elsa src, test, build, doc, design, specs, solution, NUKE scripts, release automation, package catalog, feeds, root props/targets, CI, AGENTS.md, LICENSE, icons, or copyright metadata.
  - Do not copy generated .claude/.github OpenSpec integrations unless the manifest proves they are intentional DX-OS-owned inputs; prefer later regeneration.
  - Do not invent a DX-OS remote or license, create a public repository, merge, push, or publish.
  - Do not modify Elsa source, add Elsa NuGet/runtime integration, repair the build, replace tests, or implement any business behavior in this task.
  - Do not silently discard or renumber OpenSpec/Beads state.
  - Do not weaken the accepted specification; stop and report a real conflict.

implementation_steps:
  - step: 1
    task: BR001-R1.1
    actions:
      - Re-read every source-of-truth file and run bd.cmd show open_source-cab.1 --json before mutation.
      - Record fresh source absolute path, revision, branch, remotes, status, and candidate file list; the fresh snapshot supersedes only drift in the current_state facts.
      - Create inventory.csv and extraction-manifest.json with source relative path, classification, action copy/recreate/exclude, owner/provenance, reason, size, and SHA-256.
      - Enumerate candidates explicitly; do not use the whole checkout as a copy source.
      - Export regular Beads issues to the task evidence directory and obtain a supported Dolt-native backup/status. If a backup destination is already configured, inspect it before changing configuration.
      - Scan candidate text/evidence for secret-like data; stop and report rather than copying a real secret.
  - step: 2
    task: BR001-R1.2
    actions:
      - Resolve source and default target to absolute normalized paths.
      - Fail before writing if target equals/is within source, exists with any content, contains .git, or path intent is ambiguous.
      - Create the safe empty target and copy only entries marked copy, preserving relative paths and verifying SHA-256 after copy.
      - Recreate only the minimal root files listed in allowed_scope; mark the repository NOT_READY and do not recreate R2-R7 deliverables early.
      - Reconcile every target file to a manifest entry/action and prove all exclude patterns are absent.
  - step: 3
    task: BR001-R1.3
    actions:
      - Initialize a new local Git repository/default branch in the target. Never copy old .git or configure the Elsa remote.
      - Initialize Beads non-interactively with DX-OS-owned configuration, dry-run import of beads-issues.jsonl, then import only if issue IDs/dependencies/spec links remain exact. Stop if the tool would renumber or discard state.
      - Verify open_source-cab, open_source-cab.1 through .8, dependency cycles, and spec_id links.
      - After all manifest and forbidden-content checks pass, create exactly one local initial extraction commit. Do not push or add an unapproved remote.
  - step: 4
    task: BR001-R1.4
    actions:
      - Recheck old checkout revision/remotes and explain all permitted status changes caused by evidence/Beads export.
      - Verify new Git/remote/tree/solution/project references/Beads state and manifest hashes.
      - Write implementation-report.md and verification.md in the old evidence directory and copy them to the same path in the new repository.
      - Return the structured report below and state explicitly: do not merge; do not push; old checkout preserved.

requirements:
  - The inventory is exhaustive for every copied/recreated target file and is reviewable before/after extraction.
  - The new repository must not depend on any path, project, Git object, package feed, or build artifact from the old checkout.
  - Beads IDs and OpenSpec paths remain stable even though repository ownership changes.
  - Known false-green scripts/placeholders may be preserved only as DX-OS remediation inputs and must remain labeled NOT_READY; they are not PASS evidence.
  - Use supported Beads export/import/backup commands; do not manipulate Dolt storage files directly.
  - All commands must be non-interactive or bounded and all failures must remain visible.

acceptance_criteria:
  - Source snapshot, reviewed inventory, manifest, hashes, Beads export, and backup/status evidence exist.
  - Target was proven safe/empty/outside the old checkout before first write and defaults to the specified dx-os sibling.
  - Every target file maps to an approved copy or recreate decision; forbidden/excluded content is absent.
  - Old Elsa checkout still exists at the same revision/branch/remotes, with only documented evidence/Beads state changes.
  - New Git history begins locally with the DX-OS extraction commit and contains no Elsa history; remote is an approved DX-OS URL or absent with REMOTE_PENDING_OWNER_AUTHORIZATION.
  - DXOS.slnx contains only intentional DXOS project paths and no project/reference path resolves into the Elsa checkout.
  - New repository contains no Elsa source tree, Elsa.sln, upstream build/release infrastructure, preview/private feeds, inherited root metadata, bin/obj, or secret.
  - New Beads state returns the epic and all eight children with the dependency mapping from tasks.md and no cycles.
  - implementation-report.md and verification.md contain exact commands, exit codes, file lists, limitations, and evidence hashes in both repositories.
  - No business feature, build/runtime remediation, remote push, or old-checkout deletion occurred.

security_requirements:
  - Normalize and compare absolute paths before all writes; never use a broad recursive delete/move.
  - Refuse symlinks/junctions/reparse points that escape the approved source or target boundary unless individually reviewed and recorded.
  - Treat .env, user secrets, tokens, credentials, connection strings, signing keys, Git credentials, and Beads memories as non-copyable until proven synthetic/public.
  - Do not print secrets in command output, reports, manifests, prompts, Git commit, or Beads export.
  - Hash copied files and evidence with SHA-256; record tool/version and timestamp.
  - Do not execute source-controlled scripts from unreviewed Elsa or generated directories as part of copying.

oss_requirements:
  - Record provenance/ownership for every copied document, script, source, test, rule, and skill.
  - Do not redistribute Elsa source, docs, icons, build files, copyright headers, or LICENSE as DX-OS property.
  - Elsa may be mentioned only as upstream reference/deferred MIT NuGet dependency; no Elsa package is integrated in R1.
  - Do not assert a DX-OS source license without Product Owner approval; use LICENSE-PENDING.md and block public push/READY.
  - Preserve required attribution for any reviewed third-party material or exclude it when ownership/license cannot be established.
  - Mark current OPEN_SOURCE.md/THIRD_PARTY_NOTICES.md as provisional NOT_READY if copied; final reconciliation belongs to BR001-R7.

data_requirements:
  - No business or customer data is in scope.
  - Beads regular issues/dependencies/comments/spec links are project state and must round-trip exactly.
  - Beads memories and infrastructure records are excluded by default unless individually reviewed as necessary and non-sensitive.

required_tests:
  - path safety and empty-target negative checks
  - manifest completeness and SHA-256 round-trip
  - forbidden path/file/pattern checks
  - Git identity/history/remote checks
  - Beads dry-run import, exact issue/dependency/spec-link checks, and cycle check
  - old-checkout preservation comparison
  - no unit/integration/architecture/E2E product tests are introduced in R1

verification_commands:
  - 'git -C <old_checkout> rev-parse HEAD'
  - 'git -C <old_checkout> branch --show-current'
  - 'git -C <old_checkout> remote -v'
  - 'git -C <old_checkout> status --short'
  - 'git -C <new_repository> rev-parse --show-toplevel'
  - 'git -C <new_repository> rev-list --count HEAD'
  - 'git -C <new_repository> log --oneline --decorate --all'
  - 'git -C <new_repository> remote -v'
  - 'git -C <new_repository> status --short'
  - 'git -C <new_repository> ls-files'
  - 'dotnet sln <new_repository>\DXOS.slnx list'
  - 'rg -n --glob ''*.csproj'' ''ProjectReference|PackageReference|Elsa'' <new_repository>'
  - 'rg --files <new_repository> | rg ''(^|[\\/])(bin|obj|TestResults|src[\\/]modules[\\/]Elsa|Elsa\.sln|build)([\\/]|$)'''
  - 'rg -n ''elsa-workflows/elsa-core|elsa-preview-feedz|f.feedz.io'' <new_repository> --glob ''!DXOS_CODEX_MASTER_HANDOFF.md'' --glob ''!docs/audits/**'' --glob ''!openspec/**'' --glob ''!artifacts/**'''
  - 'bd.cmd -C <new_repository> show open_source-cab --json'
  - 'bd.cmd -C <new_repository> show open_source-cab.1 --json'
  - 'bd.cmd -C <new_repository> dep cycles'
  - '<manifest verifier command> --source <old_checkout> --target <new_repository> --manifest <manifest>'

required_output:
  files:
    - artifacts/task-runs/open_source-cab.1/inventory.csv
    - artifacts/task-runs/open_source-cab.1/extraction-manifest.json
    - artifacts/task-runs/open_source-cab.1/beads-issues.jsonl
    - artifacts/task-runs/open_source-cab.1/implementation-report.md
    - artifacts/task-runs/open_source-cab.1/verification.md
  report_sections:
    - old and new absolute paths
    - source revision/branch/remotes/status before and after
    - changed/created files in each repository
    - inventory totals by copy/recreate/exclude and manifest SHA-256
    - Git initialization/commit/remote result
    - Beads export/backup/import result and exact issue/dependency comparison
    - commands actually executed with exit codes and durations
    - security/provenance/OSS findings
    - assumptions and deviations from the contract
    - known limitations and external blockers, especially remote/license
    - rollback/recovery status
    - explicit statements: old checkout preserved; do not merge; do not push
  completion_rule: >-
    Return completion for Codex review only after all acceptance criteria are
    evidenced. Your narrative is not PASS. Codex will independently inspect the
    actual repositories, diff/status, structure, Git/Beads/OpenSpec state,
    commands, exit codes, security, and OSS provenance, then issue exactly PASS
    or FIX_REQUIRED.
```
