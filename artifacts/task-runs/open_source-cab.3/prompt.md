Implement BR001-R4 Engineering Runtime Spike in:

C:\Users\199X\OneDrive\Máy tính\olympic\dx-os

OpenSpec change:
bootstrap-remediation-001

Beads issue:
open_source-cab.3

Baseline:
- Expected branch: main
- Expected clean HEAD: 1ecda7cdfa727a393b22654967e2ac7014ab2841
- Expected remote: https://github.com/ROYCE-8425/open_source.git
- BR001-R2 and BR001-R3 are closed and independently accepted.
- BR001-R4.1 through BR001-R4.4 are currently unchecked.
- Overall project remains NOT_READY.

This is implementation work, not a Codex review or publication operation.

## Mandatory preflight

Before modifying anything:

1. Run:
   - git rev-parse --show-toplevel
   - git rev-parse HEAD
   - git branch --show-current
   - git remote -v
   - git status --short --untracked-files=all
   - dotnet --version
   - docker version
   - docker info
   - docker compose version
   - openspec.cmd validate bootstrap-remediation-001 --type change --strict --no-interactive
   - bd.cmd show open_source-cab.3 --json
   - bd.cmd show open_source-cab.7 --json
   - bd.cmd dep cycles

2. Stop immediately if:
   - HEAD differs from the expected baseline;
   - the working tree is not clean;
   - the Git root is not the independent dx-os repository;
   - BR001-R3 is not closed;
   - open_source-cab.3 is not open and ready;
   - SDK 10.0.302 does not resolve normally;
   - Docker Engine or Docker Compose is unavailable;
   - OpenSpec is invalid.

Do not install tools, weaken SDK policy, change the remote, or silently skip Docker.

3. Save this exact approved prompt before implementation at:

   artifacts/task-runs/open_source-cab.3/prompt.md

4. Claim the issue:

   bd.cmd update open_source-cab.3 --claim

## Required context

Read every context file returned by:

openspec.cmd instructions apply --change bootstrap-remediation-001 --json

At minimum read:

- openspec/changes/bootstrap-remediation-001/proposal.md
- openspec/changes/bootstrap-remediation-001/design.md
- openspec/changes/bootstrap-remediation-001/tasks.md
- openspec/changes/bootstrap-remediation-001/specs/engineering-runtime/spec.md
- openspec/changes/bootstrap-remediation-001/specs/quality-evidence/spec.md
- openspec/changes/bootstrap-remediation-001/specs/repository-foundation/spec.md
- Directory.Packages.props
- Directory.Build.props
- NuGet.Config
- DXOS.slnx
- scripts/check-contract.json
- scripts/check.ps1
- scripts/verify-check-contract.ps1
- every production .csproj
- current packages.lock.json files

OpenSpec is authoritative. Do not weaken it to fit an implementation.

## Objective

Implement all four BR001-R4 tasks:

- BR001-R4.1: approved runtime dependencies and composition boundaries
- BR001-R4.2: PostgreSQL bootstrap persistence and health
- BR001-R4.3: deterministic Elsa workflow smoke
- BR001-R4.4: independent Aspire and Docker Compose runtime paths

This is an engineering runtime spike only. Do not add a business feature.

## Architecture requirements

Preserve this dependency direction:

- DXOS.Domain: no DX-OS or framework dependency
- DXOS.Application -> DXOS.Domain
- DXOS.Workflows -> DXOS.Application + stable Elsa NuGet
- DXOS.Infrastructure -> DXOS.Application + DXOS.Domain + EF/Npgsql
- DXOS.Api -> Application + Infrastructure + Workflows
- DXOS.AppHost -> DXOS.Api + Aspire hosting packages

Do not:

- reference, copy, compile, vendor, or modify Elsa source;
- reference any project or path from the old open_source/Elsa checkout;
- add a generic Repository<T>;
- add a UnitOfWork wrapper over EF Core;
- add empty IService/Service pairs;
- create another project without a durable boundary;
- introduce microservices, Kafka, Redis, RabbitMQ, or paid services;
- put EF, Npgsql, Elsa, or Aspire types in Domain;
- add product endpoints, CRM entities, identity models, campaigns, AI behavior, UI, or E2E work.

## BR001-R4.1 — Runtime dependencies

Use Central Package Management only.

Required stable versions:

- Elsa: 3.7.1
- Microsoft.EntityFrameworkCore: 10.0.10
- Microsoft.EntityFrameworkCore.Design: 10.0.10, only if required for migrations
- Npgsql.EntityFrameworkCore.PostgreSQL: 10.0.3
- Aspire.Hosting.AppHost: 13.4.6
- Aspire.Hosting.PostgreSQL: 13.4.6
- any additional health/composition package only when mechanically necessary and version-aligned

Requirements:

- NuGet.org remains the only feed.
- No preview/floating version.
- No package version inside ordinary PackageReference elements.
- No Elsa project/source reference.
- Add each package only to its intended edge.
- Verify package names and APIs against stable official documentation and actual NuGet metadata.
- Record source, resolved version, license expression/license URL, purpose and consuming project.
- Do not claim final OSS reconciliation; BR001-R7 remains responsible for final notices and SBOM.

Lock generation is a one-time implementation action:

dotnet restore DXOS.slnx --force-evaluate

After locks are generated, final verification must use only:

dotnet restore DXOS.slnx --locked-mode

Hash all lock files before and after final verification and prove zero changes.

## BR001-R4.2 — PostgreSQL persistence and health

Use PostgreSQL 18.4.

Implement only a minimal non-business bootstrap persistence model, such as a runtime probe or engineering checkpoint.

Requirements:

- DbContext belongs in DXOS.Infrastructure.
- DbContext is the EF Core unit-of-work; do not wrap it.
- Add a reviewed EF migration.
- Provide a documented, executable migration path.
- If dotnet-ef is required, use a repository-local pinned tool manifest; do not depend on an undocumented global installation.
- Liveness and readiness must be different:
  - liveness does not require PostgreSQL;
  - readiness performs a real PostgreSQL operation.
- When PostgreSQL is unavailable:
  - liveness remains healthy;
  - readiness becomes unhealthy;
  - dependent runtime/workflow smoke exits non-zero.
- No EF InMemory database counts as proof.
- No real password, connection string or secret may be committed or logged.
- Runtime credentials must come from environment variables, local secrets or Aspire-generated development configuration.
- Any disposable credential generated by automation must remain process-local and redacted from evidence.

Expected engineering endpoints may include:

- /health/live
- /health/ready

Do not add product API behavior.

## BR001-R4.3 — Elsa smoke workflow

Consume Elsa only through the stable NuGet package.

Implement one deterministic, code-defined, non-business workflow.

The smoke must:

- start a real Elsa workflow instance;
- await a terminal result within a bounded timeout;
- return a real workflow instance ID;
- return terminal status Completed;
- produce a known deterministic output;
- include a caller-supplied or generated correlation ID;
- expose enough structured evidence to correlate invocation, workflow and logs;
- exit non-zero when PostgreSQL or another required runtime dependency is unavailable.

An automation-only endpoint is allowed only when:

- it is clearly named as an engineering/internal smoke surface;
- it is disabled unless an explicit engineering-smoke configuration flag is enabled;
- it is not presented as a product API;
- it accepts no secret or PII payload.

Do not fake completion, return a hardcoded instance ID, start background work without awaiting completion, add Studio/UI, or modify Elsa internals.

## BR001-R4.4 — Aspire and Docker Compose

Aspire role:

- developer orchestration and observability;
- real DXOS.AppHost;
- PostgreSQL resource;
- DXOS.Api project resource;
- explicit references and readiness ordering;
- deterministic discoverable API endpoint;
- logs/traces/health accessible through documented commands or Aspire facilities.

Docker Compose role:

- reproducible local/demo deployment;
- independent of Aspire;
- no path outside the repository;
- no bind mount to the old Elsa checkout;
- exact PostgreSQL 18.4 tag;
- no mutable latest tag;
- health checks and bounded dependency startup;
- DX-OS API image built from a repository-owned multi-stage Dockerfile;
- no committed credential.

Before using .NET container tags, verify the official tags exist. Pin explicit versions and record resolved image digests.

Create:

- compose.yaml
- required repository-owned Dockerfile(s)
- scripts/smoke-runtime.ps1
- concise runtime documentation, preferably docs/runtime.md
- .env.example containing variable names and safe instructions, never a usable real secret

## Runtime smoke script

scripts/smoke-runtime.ps1 must support:

- -Mode Compose
- -Mode Aspire

It must:

- validate the canonical Git root;
- use bounded startup and request timeouts;
- capture native exit codes;
- use a unique run ID;
- own and identify every process/container it starts;
- wait for PostgreSQL readiness;
- wait for API liveness and readiness;
- invoke the real Elsa workflow smoke;
- validate instance ID, Completed status, deterministic output and correlation ID;
- capture sanitized logs and resource states;
- exit non-zero on any failure;
- clean task-owned processes and containers in try/finally;
- fail non-zero if cleanup fails;
- never kill unrelated processes or containers;
- delete a volume only if it was created by the current run, has an exact validated name/label and is explicitly disposable.

Aspire and Compose must each work without requiring the other path at runtime.

## Quality-gate integration

Update scripts/check-contract.json only as needed to activate the completed R4 checks.

Requirements:

- Preserve every accepted R3 fail-fast/native/path-safety behavior.
- R4 READY gates must execute before the first R5-only NOT_IMPLEMENTED gate.
- After R4 completion, Runtime and Full should:
  - pass Foundation gates;
  - execute the applicable R4 runtime/API/PostgreSQL/Elsa/Compose/Aspire checks;
  - then fail exactly at the first still-unimplemented R5 test gate.
- Do not falsely mark R5, security, SBOM, governance, CI or clean-clone gates READY.
- Update scripts/verify-check-contract.ps1 only when necessary to support the evolved contract.
- Preserve all ten accepted R3 regression assertions, using controlled fixtures rather than deleting coverage.
- Do not build another evidence framework.

## Required verification

At minimum execute and record:

1. dotnet --version
2. dotnet nuget list source
3. dotnet restore DXOS.slnx --locked-mode
4. dotnet format whitespace DXOS.slnx --verify-no-changes --no-restore
5. dotnet build DXOS.slnx -c Release --no-restore -warnaserror
6. dotnet list DXOS.slnx package --include-transitive
7. exact per-project reference enumeration
8. scan all .csproj/root configuration for:
   - Elsa source/project paths
   - open_source paths
   - preview/floating versions
   - project-local PackageReference versions
9. migration list and migration application result
10. healthy PostgreSQL database operation
11. /health/live and /health/ready with PostgreSQL available
12. /health/live and /health/ready with PostgreSQL unavailable
13. deterministic Elsa smoke success
14. Elsa/dependency-negative smoke with non-zero exit
15. docker compose -f compose.yaml config
16. scripts/smoke-runtime.ps1 -Mode Compose
17. scripts/smoke-runtime.ps1 -Mode Aspire
18. image tag and digest inspection
19. process/container/volume state before and after both modes
20. scripts/verify-check-contract.ps1
21. scripts/check.ps1 -Profile Foundation
22. scripts/check.ps1 -Profile Runtime
23. scripts/check.ps1 -Profile Full
24. openspec.cmd validate bootstrap-remediation-001 --type change --strict --no-interactive
25. bd.cmd show open_source-cab.3 --json
26. bd.cmd dep cycles
27. git diff --check
28. git status --short --untracked-files=all
29. secret-pattern scan across source, config and evidence

Expected gate semantics:

- Foundation: exit 0.
- Direct R4 smoke commands: exit 0 for positive Compose and Aspire paths.
- Controlled negative dependency tests: expected non-zero and must be asserted.
- Runtime and Full may remain non-zero only at the exact first R5 NOT_IMPLEMENTED gate after every applicable R4 gate passed.

Do not report Runtime/Full as PASS while R5 remains unimplemented.

## Evidence requirements

Store all R4 evidence under:

artifacts/task-runs/open_source-cab.3/

Required files:

- prompt.md
- implementation-report.md
- verification.md
- raw command transcripts
- runtime resource/health table
- package-and-license-delta.md or equivalent evidence
- migration evidence
- Compose smoke evidence
- Aspire smoke evidence
- negative dependency evidence
- cleanup evidence
- conventional SHA-256 sidecar with exact relative paths
- reports.sha256 for final Markdown report hashes

Evidence must record:

- exact baseline and final Git revision;
- exact changed-file list;
- commands and argument vectors;
- exit codes and durations;
- resolved package graph;
- package license metadata sources;
- nine lock paths, sizes and before/after hashes;
- migration IDs/results;
- health endpoints, HTTP status and bounded durations;
- Elsa instance ID/status/output/correlation;
- Compose resource names, image tags and digests;
- Aspire resource state;
- sanitized log locations;
- cleanup state;
- limitations and remaining NOT_IMPLEMENTED gates.

Use one final frozen verification run.

Do not embed impossible self-hashes. Use an external sidecar after reports are finalized.

Do not copy raw chat history into project evidence.

## Completion and review boundary

When implementation is ready:

- Keep open_source-cab.3 in_progress.
- Keep BR001-R4.1 through BR001-R4.4 unchecked.
- Do not close Beads.
- Do not commit.
- Do not push.
- Do not add/change a remote.
- Do not start BR001-R5.
- Do not implement a business feature.
- Do not modify the old Elsa checkout.
- Do not change repository visibility.
- Do not expose or generate GitHub credentials.

Return to Codex with:

- exact changed-file list;
- architecture/package summary;
- migration summary;
- Compose and Aspire results;
- health and Elsa smoke results;
- negative dependency results;
- cleanup proof;
- command/exit/duration matrix;
- artifact hashes;
- known blockers or limitations;
- declarations that R4 remains in_progress and no commit/push occurred.

Codex will independently inspect the repository and issue PASS or FIX_REQUIRED.
