# DX-OS: Open-Source AI-Native Marketing Operating System

[![License: Apache-2.0](https://img.shields.io/badge/License-Apache_2.0-blue.svg)](https://opensource.org/licenses/Apache-2.0)
[![REUSE status](https://img.shields.io/badge/REUSE-compliant-green.svg)](https://reuse.software)
[![Local Build](https://img.shields.io/badge/local_build-passing-brightgreen.svg)]()
[![Security Scanning](https://img.shields.io/badge/security-gitleaks%20%7C%20trivy%20%7C%20grype-blue.svg)]()
[![SBOM](https://img.shields.io/badge/SBOM-CycloneDX%201.7-blue.svg)](artifacts/sbom.cdx.json)
[![Status](https://img.shields.io/badge/status-bootstrap_remediation-orange.svg)](docs/PROJECT_STATE.md)

> **Current Status**: `NOT_READY` (Bootstrap Remediation).
> DX-OS is an independent, sovereign open-source software project licensed under the [Apache-2.0 License](LICENSE). It is **not** Elsa and is **not** an undisclosed Elsa fork. DX-OS integrates Elsa workflow capabilities as a clean NuGet dependency (`Elsa 3.7.1`).

### Dual-stack (read this)

This repository **inherits** the DX-OS .NET 10 product OS from [`ROYCE-8425/open_source`](https://github.com/ROYCE-8425/open_source) and **keeps** the AIECOS Social CRM Node foundation (MIT) as the Pancake ingest / CRM demo path. Do not delete `chrome-extension/`, `sync-receiver/`, `admin-ui/`, or `mcp-server/`.

| Stack | License | How to run | Ports |
|---|---|---|---|
| **DX-OS** (.NET 10 + Elsa + Aspire) | Apache-2.0 | `cp .env.example .env` then `docker compose -f compose.yaml up -d` | API **8080**, Postgres **5432** |
| **AIECOS** (Node + PostgREST + extension) | MIT (AIECOS) | `docker compose -f docker-compose.yml up -d` | Admin UI **8081**, PostgREST **3000**, sync **3500**, Postgres **5433** |

- Canonical DX-OS ADRs: [`docs/adr/`](docs/adr/) (0001–0007 from the DX-OS base, **0008** dual-stack).
- Competition / Gemini handoff plan: [`docs/architecture/DXOS_MASTER_PLAN.md`](docs/architecture/DXOS_MASTER_PLAN.md).
- AIECOS original docs: [`docs/aiecos/`](docs/aiecos/).
- Public product repo: [`ROYCE-8425/Marketing_social`](https://github.com/ROYCE-8425/Marketing_social).

---

## 1. What is DX-OS?

**DX-OS** is an open-source, AI-native operating system designed for Small and Medium Enterprises (SMEs) to orchestrate, automate, and govern their marketing workflows, campaign pipelines, customer analytics, and AI agent executions within a sovereign, self-hosted environment.

---

## 2. Who is DX-OS For?

- **SMEs & Growth Teams**: Organizations seeking enterprise-grade marketing automation without recurrent per-seat vendor fees or cloud lock-in.
- **Engineers & Architects**: Developers building complex, event-driven marketing workflows and autonomous AI agents with full code inspection and auditability.
- **Privacy-Conscious Organizations**: Enterprises requiring strict data sovereignty where customer data and proprietary campaign strategies never leave their self-hosted infrastructure.

---

## 3. What Problem Does It Solve?

Traditional marketing tech stacks suffer from:
1. **SaaS Fragmentation**: Marketing data trapped across disparate CRM, email, social, and analytics tools.
2. **Ungoverned AI Agents**: Hallucination risks, unmonitored API calls, and unpredictable autonomous agent behaviors.
3. **Vendor Lock-in & Prohibitive Costs**: Escalating per-contact and per-seat pricing models.
4. **Data Sovereignty Violations**: Customer analytics and PII exported to third-party proprietary clouds.

DX-OS solves this by delivering a **unified, self-hosted modular monolith** with deterministic workflow execution, governed AI agent boundaries, and end-to-end PostgreSQL persistence.

---

## 4. Current Implementation Status

| Milestone / Capability | Status | Description |
| :--- | :---: | :--- |
| **BR001-R1**: Repository Identity & Provenance | **ACCEPTED** | Independent DX-OS provenance, Apache-2.0 licensing, clean Git history. |
| **BR001-R2**: Foundation & Build Automation | **ACCEPTED** | .NET 10 Release compilation, deterministic CPM package locks, zero warnings. |
| **BR001-R3**: Quality Gate Engine | **ACCEPTED** | 26 automated gates covering format, hygiene, tests, and security scans. |
| **BR001-R4**: Runtime Startup & Smoke Orchestration | **ACCEPTED** | Docker Compose & .NET Aspire orchestration with PostgreSQL 18.4. |
| **BR001-R5**: Testing Suite | **ACCEPTED** | Unit tests (xUnit.net v3), ArchUnitNET architecture tests, Testcontainers PostgreSQL integration tests. |
| **BR001-R6**: Governance & Architectural Decision Records | **ACCEPTED** | 7 ADRs, OpenSpec governance, and machine-readable state tracking. |
| **BR001-R7**: Security, Supply Chain & OSS Inventory | **IMPLEMENTED_LOCALLY / AWAITING_INDEPENDENT_REVIEW** | Gitleaks secret scanning, Trivy misconfig checks, Syft CycloneDX SBOM, Grype vulnerability scans. |
| **BR001-R8**: Clean Clone & Public Audits | *NOT STARTED* | Public repository transition, verification badges, and external audits. |

---

## 5. Architecture Overview

DX-OS is architected as a **Modular Monolith** adhering to Clean Architecture principles:

```text
               ┌─────────────────────────────────────────────────┐
               │                 DX-OS AppHost                   │
               │         (.NET Aspire / Docker Compose)          │
               └───────────────┬─────────────────┬───────────────┘
                               │                 │
               ┌───────────────▼────────┐ ┌──────▼───────────────┐
               │        DX-OS Api       │ │   PostgreSQL 18.4    │
               │  (ASP.NET Core / Elsa) │ │     (Database)       │
               └───────────────┬────────┘ └──────────────────────┘
                               │
               ┌───────────────▼────────────────┐
               │       DX-OS Application        │
               │  (Use Cases & Orchestration)   │
               └───────┬────────────────┬───────┘
                       │                │
       ┌───────────────▼────────┐ ┌─────▼────────────────────────┐
       │      DX-OS Domain      │ │      DX-OS Workflows         │
       │   (Entities & Rules)   │ │  (Elsa Activity Definitions) │
       └────────────────────────┘ └──────────────┬───────────────┘
                                                 │
                               ┌─────────────────▼───────────────┐
                               │     DX-OS Infrastructure        │
                               │ (EF Core Migrations & Npgsql)   │
                               └─────────────────────────────────┘
```

- `src/DXOS.Domain`: Pure domain models, value objects, and business invariant validations.
- `src/DXOS.Application`: Application commands, queries, and business workflow interfaces.
- `src/DXOS.Workflows`: Elsa 3.7 workflow definitions, custom marketing activities, and execution graphs.
- `src/DXOS.Infrastructure`: PostgreSQL database contexts, EF Core 10 mappings, and data access.
- `src/DXOS.Api`: HTTP API controllers, middleware, and Elsa dashboard/server endpoints.
- `src/DXOS.AppHost`: .NET Aspire distributed application orchestrator for local and containerized runs.

---

## 6. Quick Start (Under 5 Minutes)

### Prerequisites
- [Docker](https://docs.docker.com/get-docker/) & Docker Compose
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (optional if using Docker only)

### Launching with Docker Compose

```bash
# 1. Clone the public product repository
git clone https://github.com/ROYCE-8425/Marketing_social.git dx-os
cd dx-os

# 2. Configure local secrets (never commit .env)
cp .env.example .env
# set POSTGRES_PASSWORD in .env

# 3. Start DX-OS PostgreSQL and API (compose.yaml)
docker compose -f compose.yaml up -d --build

# 4. Verify health endpoint
curl -f http://localhost:8080/health
```

Optional AIECOS Social CRM demo (does not require Facebook/TikTok/Zalo credentials):

```bash
docker compose -f docker-compose.yml up -d
# Admin UI: http://localhost:8081
# Sync receiver: http://localhost:3500/api/status
```

The DX-OS API is at `http://localhost:8080`. PostgreSQL is at `localhost:5432` (or `POSTGRES_PORT`).

Open the operator board (Vietnamese, VND): [http://localhost:8080/](http://localhost:8080/). Visual rules: [docs/design.md](docs/design.md). Product board: [docs/PRODUCT_BOARD.md](docs/PRODUCT_BOARD.md).

Lead-to-CPL slice (`NOT_READY`, no live ads): send `X-DXOS-Role` and `X-DXOS-Actor`. Endpoints: `POST /campaigns`, `POST /campaigns/{id}/submit-review`, `POST /campaigns/{id}/approve`, `POST /campaigns/{id}/reject`, `GET /campaigns/{id}`, `POST /leads/webhook`, `GET /leads`, `POST /leads/{id}/claim`, `GET /dashboard/cpl`. DX-OS stops at Lead/CPL and does not close orders.

---

## 7. Building from Source

To compile and verify DX-OS locally:

```bash
# Restore dependencies with locked CPM packages
dotnet restore --locked-mode

# Build entire solution in Release mode with zero warnings
dotnet build -c Release --no-restore
```

For comprehensive instructions on dependencies and prerequisites, refer to [docs/build-from-source.md](docs/build-from-source.md).

---

## 8. Running Tests & Quality Gates

DX-OS enforces a strict, fail-fast quality gate system. No paid tools or proprietary accounts are required.

### Running Test Projects Directly

```bash
# Run unit tests (xUnit v3)
dotnet test tests/DXOS.Unit.Tests

# Run architectural boundary tests (ArchUnitNET)
dotnet test tests/DXOS.Architecture.Tests

# Run PostgreSQL integration tests (Testcontainers)
dotnet test tests/DXOS.Integration.Tests
```

### Running the Complete Quality Gate Engine

```powershell
# Run all 26 foundation, runtime, and security gates
powershell -ExecutionPolicy Bypass -File .\scripts\check.ps1 -Profile Full
```

---

## 9. Third-Party Dependencies & Services

- **Upstream Open-Source Packages**:
  - `Elsa 3.7.1` (MIT License) - Workflow orchestration engine.
  - `Microsoft.EntityFrameworkCore 10.0.10` (MIT License) - Data modeling and persistence.
  - `Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3` (PostgreSQL License) - PostgreSQL provider.
  - `Aspire.Hosting 13.4.6` (MIT License) - Microservice orchestration.
- **Third-Party Services**:
  - All runtime operations function completely independently without mandatory external cloud services.
  - AI model gateways support self-hosted models or optional provider integrations (e.g. Google Gemini, OpenAI) disclosed in [docs/THIRD_PARTY_SERVICES.md](docs/THIRD_PARTY_SERVICES.md).

For the full dependency inventory and license breakdown, see [OPEN_SOURCE.md](OPEN_SOURCE.md) and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).

---

## 10. Roadmap

- [x] **Phase 1: Foundation & Identity** (Clean modular architecture, CPM locks, Apache-2.0 licensing)
- [x] **Phase 2: Automated Quality Gates** (26 automated CI/CD and security gates)
- [x] **Phase 3: Runtime & Testing** (Testcontainers PostgreSQL integration, Aspire orchestration)
- [ ] **Phase 4: Visual Workflow Studio** (Embedded Elsa web dashboard with custom marketing activities)
- [ ] **Phase 5: Marketing Connectors** (Integrations for Email, Webhooks, CRM, and Social channels)
- [ ] **Phase 6: Governed AI Execution Gateway** (Policy-bounded LLM agents with prompt logging and audit trails)

See [ROADMAP.md](ROADMAP.md) for detailed release milestones.

---

## 11. Contributing

We welcome contributions from the global open-source community!

- To get started, please read our [CONTRIBUTING.md](CONTRIBUTING.md) guide.
- We adhere to the [Contributor Covenant 2.1](CODE_OF_CONDUCT.md).
- Architectural changes must follow our OpenSpec and ADR process outlined in [GOVERNANCE.md](GOVERNANCE.md).

---

## 12. Security & Vulnerability Reporting

Security is a primary design pillar for DX-OS. All code undergoes automated Gitleaks secret detection, Trivy misconfiguration analysis, and Grype vulnerability scanning.

If you discover a potential security vulnerability, please follow our coordinated disclosure policy detailed in [SECURITY.md](SECURITY.md). Please **do not** report security vulnerabilities via public GitHub issues.

---

## License

DX-OS is distributed under the terms of the **Apache License, Version 2.0**.
See [LICENSE](LICENSE) and [NOTICE](NOTICE) for details.
