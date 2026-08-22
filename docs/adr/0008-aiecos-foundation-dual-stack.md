# ADR-0008: Dual-Stack Architecture (AIECOS Foundation + .NET 10 DX-OS)

- **Status:** Accepted
- **Date:** 2026-08-22
- **Deciders:** Product Owner, Architecture Team

---

## Context

The repository was forked from `aiecosvietnam/aiecos-social-crm` (MIT), providing a working Pancake DOM scraper, Node.js sync receiver, static Admin UI, and MCP server querying PostgreSQL via PostgREST.

The product goal is **DX-OS Marketing** — an enterprise/SME digital operating system for marketing and sales with multi-platform connector ingestion (Facebook, TikTok, Zalo, Website, Pancake), unified identity resolution, automated lead qualification/scoring, human-in-the-loop approvals, Elsa workflow orchestration, Microsoft.Extensions.AI integration, and .NET Aspire observability.

We needed to decide whether to rewrite AIECOS from scratch in .NET or adopt a dual-stack coexistence approach.

---

## Decision

We adopt a **Dual-Stack Coexistence Architecture**:

1. **AIECOS (Node.js) remains the foundation ingest layer**: The existing Pancake Chrome extension, Node sync receiver, static Admin UI, and MCP server are preserved and functional.
2. **DX-OS (.NET 10) is built alongside in `src/`**: The target product OS will be implemented in C# / .NET 10 with clean architecture (`DXOS.Domain`, `DXOS.Application`, `DXOS.Infrastructure`, `DXOS.Workflows`, `DXOS.Api`, `DXOS.Web`, `DXOS.AppHost`), Elsa workflows, and Aspire orchestration.
3. **Docker Compose remains the primary judge & local runtime**: Aspire serves as the dev control plane; Docker Compose boots the complete stack.

---

## Keep / New / Forbidden Boundaries

### KEEP (Preserved Foundation)
- `chrome-extension/`: Pancake DOM scraper (Manifest V3).
- `sync-receiver/`: Express.js ingestion server (`:3500`).
- `admin-ui/`: Vanilla JS admin UI (`:8081`; `:8080` is DX-OS API).
- `mcp-server/`: Node.js stdio MCP query server.
- Database schema `aiecos_social` (4 tables: `pages`, `customers`, `conversations`, `messages`).
- Docker Compose services in `docker-compose.yml` (`postgres` on host 5433, `postgrest` 3000, `sync-receiver` 3500, `admin-ui` 8081).
- DX-OS runtime stays in `compose.yaml` (Postgres 5432, API 8080).

### NEW (DX-OS .NET 10 System)
- `src/DXOS.Domain`, `src/DXOS.Application`, `src/DXOS.Infrastructure`, `src/DXOS.Workflows`, `src/DXOS.Api`, `src/DXOS.Web`, `src/DXOS.AppHost`.
- `tests/DXOS.Architecture.Tests`, `tests/DXOS.Unit.Tests`, `tests/DXOS.Integration.Tests`, `tests/DXOS.E2E.Tests`.
- Canonical tenant-scoped schema (`organizations`, `social_accounts`, `platform_connections`, `customer_identities`, `leads`, `campaign_metrics`, `approvals`, `webhook_events`, `audit_logs`).
- Platform connectors via `IPlatformConnector` (Facebook, TikTok, Zalo, Mock, Pancake adapter).
- Elsa Workflows, ArchUnitNET architecture tests, Testcontainers integration tests.
- Governance & tools: OpenSpec (`openspec/`), Beads (`.beads/`), Agent rules & skills (`.agents/`).

### FORBIDDEN
- ❌ Dropping or rewriting AIECOS Node stack to "clean up" the repository.
- ❌ Creating platform-specific silo tables (e.g. `facebook_users`, `tiktok_leads`) instead of canonical domain models.
- ❌ Leaking platform SDK types into `DXOS.Domain`.
- ❌ Direct SQL or raw PostgREST queries from MCP (MCP must call `DXOS.Application` services).
- ❌ Scraping platforms from MCP or returning encrypted access tokens.
- ❌ Autonomous destructive actions by AI (publishing, spending, deleting without human approval).

---

## Consequences

- **Positive:** Zero downtime for the existing running Pancake demo; fast iterations; reliable migration path via event adapter; clean enterprise .NET architecture.
- **Trade-off:** Managing two runtimes in the repository during development until DX-OS reaches full feature maturity.
