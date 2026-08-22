# DX-OS Agent Instructions

Guidance for AI coding agents and human developers working in the DX-OS repository.

## 1. Project Identity & Ownership

- **DX-OS is an independent open-source project**: DX-OS is its own product and repository; it is not Elsa and must not present itself as an Elsa fork or derivative project.
- **Upstream Elsa Consumption**: Elsa is an upstream workflow engine consumed exclusively through approved, stable NuGet packages (CPM managed). No Elsa source code, solution files, or project references exist in DX-OS.
- **Default License**: DX-OS is licensed under Apache-2.0 by default, governed by ADR-0001.
- **Repository Status**: Bootstrap remediation phase. Business features remain gated until the clean-clone follow-up audit achieves READY.
- **AIECOS foundation (MIT)**: `chrome-extension/`, `sync-receiver/`, `admin-ui/`, `mcp-server/`, `examples/`, and `docs/aiecos/` are preserved ingest/CRM code. Do not delete, rewrite, or relicense them to Apache-2.0. See ADR-0008.
- **Public remote**: `https://github.com/ROYCE-8425/Marketing_social.git`. Do not push DX-OS product commits to `aiecosvietnam/aiecos-social-crm`.

## 2. Authority Hierarchy

When resolving requirements, architecture, or conflicts, follow this strict descending authority order:

1. **Current User Instruction**: Direct explicit prompt for the active turn.
2. **DX-OS Constitution** (`.specify/memory/constitution.md`): Mandatory, non-negotiable governance and core principles.
3. **Accepted OpenSpec Contracts** (`openspec/`): Authoritative WHAT, WHY, design, specifications, and task definitions.
4. **Architecture Decision Records (ADRs)** (`docs/adr/`): Durable, ratified technical and architectural decisions.
5. **Business Rules**: Domain invariants and functional boundaries.
6. **Beads Task Acceptance Criteria** (`bd`): Operational task contracts and dependency graph.
7. **Workspace Rules & Repository Instructions** (`.agents/rules/`, `AGENTS.md`): Engineering guidelines and conventions.
8. **Implementation Artifacts & Reports** (`artifacts/task-runs/`): Historical evidence and test outputs.
9. **Model Assumptions / Chat Transcripts**: Chat narratives and self-reports are never durable authority.

If two sources at the same or higher authority conflict, STOP and report the conflict immediately. Never silently choose an outcome.

## 3. Roles of OpenSpec and Beads

- **OpenSpec (`openspec/`)**: Defines requirements, behavioral contracts, specifications, designs, and acceptance criteria. OpenSpec answers *WHAT* is required and *WHY*.
- **Beads (`bd`)**: Tracks operational tasks, assignees, dependency ordering, blocker states, and execution status. Beads answers *WHEN* work executes and *WHO* is working on it.
- **Alignment**: Every implementation task in OpenSpec references its mapped Beads issue. Neither OpenSpec nor Beads may silently override constitutional principles.
- **Completion State**: Task checkboxes in OpenSpec and Beads issue status remain open/unchecked until independent review acceptance.

## 4. Dual-Agent Protocol (Gemini & Codex)

DX-OS employs a strict dual-agent governance model:

- **Gemini (Implementer)**:
  - Claims Beads tasks (`bd update <id> --claim`).
  - Implements scoped code and test changes strictly within task boundaries.
  - Executes local verification gates and writes evidence (`implementation-report.md`, `verification.md`).
  - Submits the task for review without checking task boxes, closing Beads issues, or committing to git.
- **Codex (Independent Reviewer)**:
  - Independently inspects repository diffs, build outputs, native exit codes, test assertions, security scans, and OSS compliance.
  - Issues an unambiguous verdict: `PASS` or `FIX_REQUIRED`.
  - Task completion, OpenSpec checkbox marking, Beads issue closure, and commit/push actions occur only after an independent PASS verdict.

## 5. Architecture & Solution Direction

- **Modular Monolith**: DX-OS is structured as a modular monolith with vertical slices.
- **Project Structure (`DXOS.slnx`)**:
  - `src/DXOS.Domain`: Pure domain models and business invariants; strictly zero infrastructure or provider SDK dependencies.
  - `src/DXOS.Application`: Application services, use cases, ports, and orchestrations.
  - `src/DXOS.Infrastructure`: Persistence (`BootstrapDbContext`), external gateways, and data migrations using PostgreSQL 18.4 and EF Core 10.
  - `src/DXOS.Workflows`: Workflow definitions and activities using Elsa 3.7.1 NuGet packages.
  - `src/DXOS.Api`: ASP.NET Core REST endpoints, health probes (`/health/live`, `/health/ready`), and service composition.
  - `src/DXOS.AppHost`: .NET Aspire 13.4 orchestration for developer inner-loop runtime.
- **Proportionate Boundaries**: Prohibit generic repository abstractions, empty `IService`/`Service` ceremony pairs, UnitOfWork wrappers over EF Core, and premature microservices.
- **Dependency Flow**: References flow strictly inward: `AppHost` -> `Api` -> `Workflows` -> `Infrastructure` -> `Application` -> `Domain`. Domain has zero external dependencies.

## 6. Safety, Quality, and Verification Gates

- **Fail Closed**: Any missing tool, unhandled exception, or non-zero exit code immediately halts execution and fails the gate.
- **Deterministic Quality Gate**: Run via `scripts/check.ps1` supporting specific profiles:
  - `Foundation`: SDK preflight, locked restore, code formatting, Release compilation (`-warnaserror`), and OpenSpec validation.
  - `Runtime`: Foundation + unit tests, ArchUnitNET architecture tests, and PostgreSQL Testcontainers integration tests.
  - `Full`: Runtime + security scans (Gitleaks, Trivy, Grype), CycloneDX SBOM validation, and clean-clone readiness.
- **Testing Standards**:
  - Unit tests: xUnit v3 / Microsoft.Testing.Platform testing real behaviors.
  - Architecture tests: ArchUnitNET verifying assembly references, domain isolation, and anti-pattern bans.
  - Integration tests: Testcontainers PostgreSQL with real schema migrations and health checks.
  - Zero-test passes and placeholder tests are strictly banned.
  - Browser E2E tests are explicitly marked N/A during bootstrap remediation.
- **Resource Hygiene**: Containerized test fixtures and scripts must clean up task-owned Docker containers, networks, and volumes without mutating unrelated resources.

## 7. Open-Source Obligations & Transparency

- **Project License**: Default license is Apache License 2.0 (`LICENSE`), recorded in ADR-0001.
- **Dependency Inventory & Notices**: Maintain `OPEN_SOURCE.md` and `THIRD_PARTY_NOTICES.md` with complete attribution for direct and transitive packages.
- **Software Bill of Materials (SBOM)**: Deliverable-derived CycloneDX JSON SBOM maintained at `artifacts/sbom.cdx.json`.
- **Third-Party Service Disclosure**: Proprietary APIs, SaaS platforms, and development AI tooling are disclosed separately in `docs/THIRD_PARTY_SERVICES.md` per ADR-0002.
- **AI Provider Independence**: AI integrations use DX-OS-owned abstractions (`IChatClient` / provider ports); domain and application modules must never couple directly to vendor SDKs.
- **Truthful Claims**: Never make unverified "100% open source" claims. Distinguish DX-OS core code, OSS packages, third-party services, and developer tooling.
- **Gated Release**: The project cannot be marked READY or released without passing the clean-clone reproducibility gate.

## 8. Docker & Deployment Requirements

- **Mandatory Containerization**: Clean-clone Docker / Docker Compose (`compose.yaml`) deployment is mandatory for release readiness.
- Direct `dotnet run` startup alone is insufficient to satisfy release criteria.

## 9. Build, Test, and Quality Commands

Execute commands directly via PowerShell:

- **Foundation Quality Gate**:
  ```powershell
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/check.ps1 -Profile Foundation
  ```
- **Runtime Quality Gate**:
  ```powershell
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts/check.ps1 -Profile Runtime
  ```
- **Solution Compilation**:
  ```powershell
  dotnet build DXOS.slnx -c Release --no-restore -warnaserror
  ```
- **Targeted Unit Tests**:
  ```powershell
  dotnet test tests/DXOS.Unit.Tests/DXOS.Unit.Tests.csproj
  ```
- **Targeted Architecture Tests**:
  ```powershell
  dotnet test tests/DXOS.Architecture.Tests/DXOS.Architecture.Tests.csproj
  ```
- **Targeted Integration Tests**:
  ```powershell
  dotnet test tests/DXOS.Integration.Tests/DXOS.Integration.Tests.csproj
  ```
- **OpenSpec Validation**:
  ```powershell
  openspec.cmd validate bootstrap-remediation-001 --type change --strict --no-interactive
  ```
- **Beads Issue Graph**:
  ```powershell
  bd.cmd dep cycles
  bd.cmd dep tree open_source-cab
  ```
