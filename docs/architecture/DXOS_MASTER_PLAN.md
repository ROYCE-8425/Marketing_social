# DX-OS Marketing — Master Architecture & Gemini Handoff Plan

**Status:** Codex/Grok owned. Not implementation.  
**Date:** 2026-08-22  
**Foundation repo (audited):** this repository, forked from `aiecosvietnam/aiecos-social-crm` (MIT).  
**Product:** DX-OS Marketing — SME digital OS for marketing/sales.  
**Loop:** Grok/Codex plans + reviews → Gemini implements + tests → Grok reviews git diff + evidence → next task.

---

## Authority (read first)

```
1. Accepted OpenSpec specification
2. Architecture Decision Records (docs/adr/)
3. This master plan
4. Business rules (docs/business/)
5. Beads task acceptance criteria
6. .agents/rules
7. .agents/skills (project)
8. Existing code patterns
9. Model assumptions
```

If two sources conflict: **STOP and report.** Do not silently choose.

Gemini does **not** own spec, architecture, package introduction, test deletion, scanner disable, or merge to main.

---

# PART A — Current architecture

## What the repository actually is

This repo is **AIECOS Social CRM**, a Pancake-DOM → Postgres Social CRM template. It is **not** DX-OS Marketing.

```
Pancake web (Zalo OA / Facebook / Instagram via Pancake UI)
        │  DOM scan (MutationObserver + auto-walk)
        ▼
chrome-extension/     Manifest V3, host pancake.vn only
        │  POST /api/sync  header X-AIECOS-Token
        ▼
sync-receiver/server.js   Express :3500, supabase-js
        │  upsert pages → customers → conversations → messages
        ▼
Postgres schema aiecos_social  +  PostgREST :3000
        │
        ├─ admin-ui/index.html     vanilla JS, client-side stage classification
        └─ mcp-server/index.js     MCP SDK stdio, 8 read-only tools
```

No queue. No worker. No cache. No OAuth. No official Facebook/TikTok/Zalo API client. No Elsa. No .NET. No Aspire.

## Folder map (source of truth)

| Path | What it actually does |
|---|---|
| `chrome-extension/` | Pancake DOM scraper (content.js selectors `[id^="message_pzl_m_"]`) |
| `sync-receiver/server.js` | Ingest API only |
| `sync-receiver/schema.sql` | 4 tables, no migrations folder |
| `admin-ui/index.html` + `demo-data.js` | Single-file CRM UI + demo mode |
| `mcp-server/index.js` | 8 CRM query tools, REST to PostgREST |
| `docker-compose.yml` | postgres:15 + postgrest + sync-receiver + nginx admin-ui |
| `docs/` | ARCHITECTURE, DEPLOY, MCP_USAGE |
| `examples/` | curl + seed via `/api/sync` |
| `.github/workflows/ci.yml` | `node --check`, npm install smoke, docker build, secret scan |
| `.github/workflows/pages.yml` | deploy admin-ui |

**Does not exist:** `src/`, `tests/`, `packages/`, `.agents/`, `openspec/`, `.beads/`, `SECURITY.md`, `THIRD_PARTY.md`, `NOTICE`, EF Core, Elsa, Aspire, ArchUnitNET.

## Runtime (verified from files)

| Concern | Actual |
|---|---|
| Backend | Node.js 20+/22, Express 4 (`sync-receiver/package.json`) |
| DB access | `@supabase/supabase-js` against PostgREST or Supabase cloud |
| Frontend | One HTML file, Chart.js + Lucide CDN, localStorage config |
| MCP | `@modelcontextprotocol/sdk` stdio |
| Auth ingest | Shared secret `API_TOKEN` / `X-AIECOS-Token` |
| Auth UI | None. Anon key in localStorage |
| Rate limit | `express-rate-limit` on `/api/sync` only (300/min) |
| License | MIT, Copyright 2026 AIECOS (`LICENSE`) |

## Data layer (verified: `sync-receiver/schema.sql`)

Tables: `pages`, `customers`, `conversations`, `messages`.

- PKs are `text`, not UUID.
- Customer id = `${pageId}_${ten_khach}` (`sync-receiver/server.js` `processOneMessage`).
- Conversation id = `${pageId}__${slug(name)}` or pancake thread id.
- Message id = sha1(`pmid:` + pancake_msg_id) or content hash.
- `pages.type` is a string channel hint (`facebook` / `zalo` / `instagram` / `other`).
- No tenant/org. No identities. No leads. No campaigns. No approvals. No sync_jobs. No webhook_events. No audit_logs.
- Indexes exist on messages/conversations/customers timestamps.
- Unique constraints = primary keys only.
- RLS: **not in SQL**. Docs mention it as a production checklist only (`SETUP.md`, `docs/DEPLOY.md`).
- No versioned migrations. One `IF NOT EXISTS` dump.

## Integration layer (verified)

`sync-receiver/server.js` endpoints:

- `GET /api/status`
- `POST /api/channel/register`
- `GET /api/channels`
- `POST /api/sync` (batch ≤200 or single)
- `POST /api/admin/refresh-aggregates`
- `GET /metrics`

Chrome extension: `chrome-extension/content.js`, `background.js`, `popup.js`. Pancake-only. Not an official platform connector.

No OAuth state. No token vault. No refresh. No webhook inbound except pretending Pancake POST is the event. No retry/backoff/DLQ. Rate limit is inbound HTTP, not outbound platform API.

## MCP layer (verified: `mcp-server/index.js`)

Tools: `summary`, `list_partners`, `get_partner_messages`, `search_messages`, `get_at_risk_partners`, `pipeline_stats`, `top_partners_by_volume`, `recent_activity`.

- Transport: stdio.
- Data access: raw PostgREST `fetch`.
- Auth: env `AIECOS_SUPABASE_KEY` (service role).
- No resources, no prompts registry, no tenant isolation, no audit.
- `examples/mcp-prompts.md` says MCP is **read-only by design**.
- Classification duplicated in MCP and UI (`classifyPartnerStage` / `stage()`), not stored.

## Frontend (verified: `admin-ui/index.html`)

Nav: Dashboard, Triage, Inbox, Pipeline, Partner 360, Performance, Reports, Help, Settings.

- Demo mode when no Supabase URL/key (`loadAll`, `DEMO_DATA`).
- Pipeline = recency buckets from `last_seen_at` (active ≤3d … churned >90d). **Not sales pipeline.**
- Live inbox pages still read `window.DEMO_DATA.pages` even in live mode (limitation).
- No login. No React/Vue. CONTRIBUTING rejects bundlers.

## Local uncommitted noise (not product)

`docker-compose.yml` maps Postgres `5433:5432`. `sync-receiver/server.js` and `admin-ui/index.html` rewrite `/rest/v1/` for local PostgREST. Treat as local workaround, not architecture.

---

# PART B — Current capabilities

### Running today

1. `docker compose up` + seed → 5 synthetic partners in 5 recency stages.
2. Admin UI demo without DB (`admin-ui/demo-data.js`).
3. Pancake message ingest + dedup + 4-table upsert.
4. Inbox / kanban / partner 360 / reports CSV-HTML.
5. MCP natural-language **read** of CRM.
6. CI syntax + docker build + JWT/secret grep.

### Looks like DX-OS, is not

| Claim | Code reality |
|---|---|
| Multi-channel FB/Zalo/IG | One Pancake DOM source; channel from URL (`parseThreadType`, `detectChannelType`) |
| Pipeline | Recency classification, not lead HOT/WARM/COLD |
| Lead qualification | Prompt in `examples/mcp-prompts.md`; no `leads` table |
| AI-ready | MCP exposes data; no scoring engine, no IChatClient |
| Demo mode | UI fixtures or Pancake-shaped `/api/sync` seed — **no mock FB/TikTok/Zalo connector** |

### Not present (must not be assumed)

Official Facebook/TikTok/Zalo APIs, OAuth, token refresh, unified identity, lead scoring/routing, approval, campaign analytics, workflow engine, tests, SECURITY.md, THIRD_PARTY notices.

---

# PART C — Technical debt / missing pieces

| Area | Debt | File evidence |
|---|---|---|
| Identity | Customer 1:1 page+name | `processOneMessage` customer key |
| Tenant | None | schema.sql |
| Events | No idempotent webhook table | schema.sql |
| Domain API | Ingest-only Express | server.js 6 routes |
| Connectors | Pancake scraper only | chrome-extension/manifest.json host_permissions |
| Scoring | Computed in UI/MCP, not persisted | `stage()` in index.html; `classifyPartnerStage` in mcp-server |
| Tests | Zero unit/integration/e2e | CI only `node --check` |
| Migrations | Single dump | schema.sql |
| OSS | No SECURITY, THIRD_PARTY, NOTICE, SBOM | root listing |
| MCP boundary | Direct REST, duplicates domain | mcp-server/index.js `rest()` |
| Secrets | API_TOKEN logged truncated; UI key in localStorage | server.js boot log; saveConfig() |
| Dual stack | Product owner now wants .NET 10; repo is Node | this ADR-0001 |

---

# PART D — Target architecture

## ADR-0001 (locked by Product Owner, recorded by Codex)

**Decision:** DX-OS product OS is **ASP.NET Core / .NET 10 + Elsa + PostgreSQL + Aspire**. AIECOS Node stack is **foundation ingest**, not deleted.

**Why not rewrite AIECOS in place:** Competition needs a running demo. Node CRM already boots. Rewriting the scraper/UI first destroys the only working path.

**Why .NET for DX-OS:** Owner-locked vibe-coding stack: Elsa workflows, ArchUnitNET, Testcontainers, Microsoft.Extensions.AI, Aspire observability.

```
AIECOS (Node, keep)
  chrome-extension + sync-receiver + existing admin-ui + existing MCP
        │  NormalizedEvent adapter (later)
        ▼
DX-OS (new .NET)
  PlatformConnector → Event intake → Domain → Elsa → IChatClient
        │
        ├─ DXOS.Api
        ├─ DXOS.Web (new UX; Taste skill)
        └─ DXOS.Mcp  (MCP → Application, never raw SQL)
```

Docker Compose remains the **judge install path**. Aspire is the **dev control plane**. They do not replace each other.

## Layers

| Layer | Responsibility | Must not do |
|---|---|---|
| Platform | Facebook, TikTok, Zalo, Website, Form, Pancake, Mock | Own business rules |
| Connector | `IPlatformConnector`: authenticate, refresh, capabilities, subscribe, fetch, normalize | Leak SDK types into Domain |
| Integration / Event | Idempotent ingest, retry, DLQ, sync_jobs | Dedup customers |
| Normalization | Map platform payload → `NormalizedEvent` | Store platform-specific tables as source of truth |
| Unified Data | Postgres canonical model, tenant-scoped | `facebook_users` style tables |
| Domain | Lead, Identity, Campaign, Approval invariants | Reference FB/TikTok/Zalo packages |
| Workflow | Elsa: assignment, SLA, human approval, versioning | Put simple CRUD in Elsa |
| AI | `IChatClient`: classify, extract, summarize, recommend | Delete data, spend money, publish, cross-tenant |
| MCP | Tool façade over Application | Raw SQL, platform scrape, return tokens |
| API | HTTP for UI/MCP/webhooks | Embed scoring rules in controllers |
| Frontend | Human control plane | Call platform APIs |

## Connector contract

```csharp
public interface IPlatformConnector
{
    string Provider { get; }
    IReadOnlySet<Capability> Capabilities { get; }
    Task AuthenticateAsync(...);
    Task RefreshTokenAsync(...);
    Task SubscribeWebhooksAsync(...);
    Task<IReadOnlyList<NormalizedEvent>> FetchDataAsync(FetchRequest req, CancellationToken ct);
    NormalizedEvent NormalizeEvent(RawPlatformPayload payload);
}
```

Capabilities are flags, not “every platform has leads”. Domain calls `Supports(Capability.ReadLeads)` — never `if (provider == "facebook")`.

Demo mode: mock connectors emit the same `NormalizedEvent`. Real mode: official API + OAuth, same event.

---

# PART E — Domain model

All business tables: `organization_id`, `created_at`, `updated_at`. Soft delete only where recovery matters (customers, leads).

**Idempotency for external events:** unique `(organization_id, provider, external_event_id)`.

Canonical tables (target, additive; do not drop AIECOS 4 tables in MVP):

- `organizations`, `users`, `memberships`
- `social_accounts`, `platform_connections` (encrypted token payload, scopes, status, expires_at, last_sync_at, capabilities jsonb)
- `customers`, `customer_identities` unique `(organization_id, provider, external_user_id)`
- `conversations`, `messages` (new UUID model; AIECOS rows mapped later)
- `leads`, `lead_sources`, `lead_score_snapshots` (score, band, reason, model, version, timestamp)
- `campaigns`, `campaign_metrics` (fetched_at, source, data_freshness)
- `approvals`, `approval_steps` (actor, decision, reason, snapshot, version)
- `workflow_runs`, `sync_jobs`, `webhook_events` (provider, external_event_id, event_type, received_at, processed_at, status, payload_hash)
- `audit_logs` (actor, action, entity, before/after, tenant)

Identity resolution: phone/email/platform id **only if permitted**. Never merge across tenants. Always keep identities even after merge.

AIECOS `customers` become one identity (`provider=pancake`) linked to a canonical customer.

---

# PART F — Integration architecture

```
DEMO:  MockConnector → NormalizedEvent → Intake (idempotent) → Domain → Elsa → Api/Web/Mcp
REAL:  OAuth → Connector → Webhook/Poll → same Intake
LEGACY: Pancake Extension → sync-receiver → adapter → same Intake (feature-flagged)
```

Every external call: timeout, retry with backoff, error state on `sync_jobs`, never swallow.

Official FB/TikTok/Zalo: **P2 after E2E demo**. Until then status is MOCK_IMPLEMENTED, not OFFICIAL_SUPPORTED.

---

# PART G — MCP architecture

```
MCP tool
  → Authorization (tenant + role)
  → DXOS.Application service
  → Domain
  → Repository
  → PostgreSQL
  → audit_logs
```

Forbidden: MCP → PostgREST directly (current AIECOS pattern must not be copied into DX-OS). Forbidden: return access tokens. Forbidden: scrape platforms.

Keep `mcp-server/` (Node) until `DXOS.Mcp` exists; then Node MCP becomes compatibility shim or is retired in a dedicated task.

---

# PART H — Security model

- Tenant on every query. ArchUnit + integration tests prove org A cannot read org B.
- Tokens: encrypted at rest, never logged, never in MCP output, never in git.
- Demo mode: no real credentials required (`Rule 7`).
- AI: classify/recommend/summarize only. Publish, spend, delete, budget change require approval capability (`dxos-ai-safety`).
- Scanners (deterministic, not optional): Gitleaks, Trivy, Syft SBOM, Grype, GitHub Dependency Review.
- AIECOS leftover: service_role in MCP env; UI key in localStorage — do not replicate in DXOS.Web.

---

# PART I — Testing strategy

| Layer | Tool | Rule |
|---|---|---|
| Architecture | ArchUnitNET | Domain ↛ Infrastructure; Domain ↛ Facebook/TikTok/Zalo packages; AI providers only in AI.Infrastructure |
| Unit | xUnit | Domain scoring, routing, idempotency, identity merge |
| Integration | Testcontainers.PostgreSql | Real Postgres; no fake repository for persistence tests |
| HTTP mocks | WireMock.Net | Platform APIs and webhooks |
| E2E | Playwright .NET | Judge demo path |
| Node legacy | keep `node --check` in CI | Do not port AIECOS tests that do not exist; add later only if touching Node |

`scripts/check.ps1` is the only quality gate Gemini must run for .NET work. `scripts/check-e2e.ps1` is slower, run on demo tasks.

**Done ≠ implemented.** Done = spec + arch tests + build + unit + integration + security scan + Codex review of **diff**.

---

# PART J — OSS / license strategy

Keep AIECOS MIT copyright. Add DX-OS copyright for new files.

Must add (P0): `SECURITY.md`, `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md`, SBOM artifact path `artifacts/sbom.cdx.json`.

Vendor skills: copy selected MIT skills into `.agents/skills/` **with SHA pin** and notices. Do not vendor entire reverse-skill / pentest packs.

Do not commit `.env`, tokens, real customer data.

---

# PART K — MVP definition

One E2E path, demo mode, no platform credentials:

```
Mock Facebook lead webhook
  → idempotent intake
  → normalize
  → identity dedup
  → rule score = 87, band HOT, persist reason/model/version/timestamp
  → auto-assign Sales + SLA
  → conversion
  → dashboard (leads + CPL/ROI with data_freshness)
  → MCP: "lead performance today by platform"
```

Same customer may also have TikTok + Zalo mock identities.

**Out of MVP:** official OAuth, Elsa publishing to social, full campaign automation, replacing admin-ui entirely on day 1, rewriting chrome-extension.

Competition mapping: practical value + automation + integration = 45 business points. AI is 15. Do not invert that.

---

# PART L — Complete task breakdown

Priorities: P0 foundation (incl. agent brain) → P1 core business → P2 mock platforms (not official) → P3 AI → P4 MCP → P5 approval → P6 polish.

Official OAuth Facebook/TikTok/Zalo is **after** MVP E2E. Do not start TASK-06x official API before TASK-038 demo seed is green.

---

## TASK-000 — ADR dual-stack + freeze AIECOS

- **Objective:** Record that AIECOS Node stays; DX-OS is new .NET OS. Prevent Gemini from rewriting the foundation.
- **Scope:** `docs/adr/0001-dual-stack-aiecos-dotnet.md`, pointer in README (short).
- **Non-scope:** Any C# or AIECOS behavior change.
- **Depends:** none
- **AC:** ADR states keep/delete lists; README one paragraph “DX-OS is being built alongside AIECOS”. `docker compose config` still valid.
- **Tests:** none (docs). CI must still pass existing Node jobs.
- **Validate:** `git diff --stat`; `docker compose -f docker-compose.yml config`
- **Risk:** Contributors “clean up” by deleting extension.
- **Rollback:** revert ADR commit.

## TASK-001 — Vendor selected agent skills (pin SHA)

- **Objective:** Put project-local skills in git so clone = agent brain.
- **Scope:** Copy **selected** skills only into `.agents/skills/`. Write `docs/THIRD_PARTY_SKILLS.md` with repo URL + commit SHA + license.
- **Non-scope:** Custom dxos-* skills (TASK-006). Installing 24/24 Addy skills. reverse-skill. Global-only MCP config (document in AGENTS.md only).
- **Depends:** TASK-000
- **Install sources (verify latest, then pin):**
  - https://github.com/addyosmani/agent-skills (MIT) — plugin 0.6.7 as of 2026-08-14
  - https://github.com/Leonxlnx/taste-skill (pin commit; v2 experimental)
  - https://github.com/UnitOneAI/SecuritySkills (MIT)
- **Addy skills to copy (only these):**  
  `incremental-implementation`, `test-driven-development`, `context-engineering`, `source-driven-development`, `doubt-driven-development`, `api-and-interface-design`, `debugging-and-error-recovery`, `code-review-and-quality`, `code-simplification`, `security-and-hardening`, `git-workflow-and-versioning`, `ci-cd-and-automation`, `documentation-and-adrs`, `observability-and-instrumentation`  
  **Do not copy:** `spec-driven-development`, `planning-and-task-breakdown` (OpenSpec + Beads own those).
- **Taste:** `design-taste-frontend` only (not all style variants).
- **SecuritySkills copy if present:**  
  `skills/appsec/threat-modeling`, `secure-code-review`, `api-security`, `dependency-scanning`,  
  `skills/ai-security/llm-top-10`, `agentic-top-10`, `prompt-injection`, `ai-data-privacy`,  
  `skills/identity/rbac-design`, `skills/cloud/container-security`  
  plus `secrets-management` and `pipeline-security` **only if those directories exist** after clone. Do not invent them.
- **AC:** Each skill has `SKILL.md`. Notices file lists SHA. No pentest/malware skills. `.gitignore` does not ignore `.agents/skills`.
- **Tests:** none
- **Validate:** list `.agents/skills`; show SHAs in notices
- **Risk:** License mismatch; bloated context if too many skills
- **Rollback:** delete `.agents/skills`

## TASK-002 — Workspace rules (authority)

- **Scope:** `.agents/rules/00-authority.md`, `10-dotnet-architecture.md`, `20-testing.md`, `30-security.md`, `40-database.md`, `50-ai-governance.md`, `60-git.md`
- **Non-scope:** Product code
- **Depends:** TASK-001
- **AC:** Authority order matches this plan. Architecture rules match ArchUnit intent. AI may not spend/publish/delete without approval.
- **Rollback:** delete `.agents/rules`

## TASK-003 — OpenSpec init

- **Scope:** `npm install -g @fission-ai/openspec@latest`; `openspec init`; first change `openspec/changes/dxos-foundation/` with proposal.md + design.md (no silent requirement edits later).
- **Non-scope:** Implementing the change
- **Depends:** TASK-002
- **Source:** https://github.com/Fission-AI/OpenSpec
- **AC:** `openspec/` exists; Gemini cannot mark implementation done without accepted spec pointer.
- **Rollback:** remove `openspec/`

## TASK-004 — Beads init + seed graph

- **Scope:** `npm install -g @beads/bd`; `bd init`; `bd setup gemini` if supported else document in AGENTS.md; create issues for TASK-000… with dependencies; `bd ready` shows next work.
- **Non-scope:** Replacing this plan
- **Depends:** TASK-003
- **Source:** https://github.com/gastownhall/beads (also historically steveyegge/beads); npm `@beads/bd`
- **AC:** `.beads/` committed per Beads docs; `bd ready --json` returns TASK-010 only after P0 brain tasks close.
- **Rollback:** `bd` docs; do not leave half-initialized issue db

## TASK-005 — AGENTS.md + OSS stubs

- **Scope:** Root `AGENTS.md`, `SECURITY.md`, `OPEN_SOURCE.md`, `THIRD_PARTY_NOTICES.md` (deps + skills). Point to AIECOS LICENSE. Do not rewrite LICENSE copyright away.
- **Depends:** TASK-001
- **AC:** Clone instructions: OpenSpec + Beads + skills live in git; global tools listed separately.
- **Non-scope:** Full SBOM generation (TASK-080)

## TASK-006 — Custom DX-OS skills (skeletons)

- **Scope:** `.agents/skills/dxos-domain`, `dxos-elsa-workflow`, `dxos-api-integration`, `dxos-ai-safety`, `dxos-database-migration`, `dxos-demo-verifier`, `dxos-oss-compliance` — SKILL.md only, progressive disclosure, no pentest.
- **Depends:** TASK-002
- **AC:** Each skill states invariants. `dxos-ai-safety` lists forbidden actions. `dxos-elsa-workflow` says when **not** to use Elsa (CRUD).
- **Non-scope:** Implementing Elsa host

## TASK-007 — Quality gate script + CI stub

- **Scope:** `scripts/check.ps1` (exists, may no-op .NET until TASK-010); GitHub Action job `dxos` that does not fail the AIECOS Node jobs.
- **Depends:** TASK-010 for real dotnet; this task can add script placeholder then TASK-010 fills it. **Order:** create script that fails clearly if sln missing; TASK-010 makes it pass.
- **AC:** Script documents intended commands. Existing `ci.yml` Node jobs unchanged.

## TASK-010 — .NET 10 solution skeleton

- **Scope:** `global.json` (.NET 10 SDK), `Directory.Build.props`, `Directory.Packages.props`, `src/DXOS.Api`, `DXOS.Application`, `DXOS.Domain`, `DXOS.Infrastructure`, `DXOS.Workflows`, `DXOS.AppHost`, `DXOS.Web` (empty), test projects. Central package management. `.editorconfig`.
- **Non-scope:** Business logic, Elsa workflows, deleting Node folders.
- **Depends:** TASK-000, TASK-002
- **AC:** `dotnet build -c Release` succeeds. No NuGet beyond BCL + ASP.NET + xunit + ArchUnitNET + EF Core + Aspire hosting (justify each in THIRD_PARTY_NOTICES).
- **Tests:** smoke test that Domain project exists.
- **Risk:** Gemini adds MediatR/AutoMapper/Hangfire without spec.
- **Rollback:** delete `src/`, `tests/`, sln

## TASK-011 — ArchUnitNET first rules

- **Depends:** TASK-010
- **AC:** Tests fail if Domain references Infrastructure or any `Facebook`/`TikTok`/`Zalo` package. CI runs Architecture.Tests.
- **Non-scope:** All future rules

## TASK-012 — Tenant + Organization persistence

- **Depends:** TASK-010, TASK-011
- **Scope:** EF Core + Postgres; `organizations`, `users`; Testcontainers integration test insert org A, cannot query as org B.
- **Non-scope:** Identity providers, JWT UI login (minimal API key or seed user is enough for demo).
- **AC:** Migration in `DXOS.Infrastructure`; `organization_id` required.

## TASK-013 — Aspire AppHost + compose coexistence

- **Depends:** TASK-012
- **Scope:** AppHost wires Api + Postgres. `compose.yaml` **adds** dxos-api without removing AIECOS services. Document ports.
- **AC:** `dotnet run --project src/DXOS.AppHost` health; `docker compose up` still serves admin-ui :8080 and sync :3500.
- **Non-scope:** Replacing nginx admin-ui

## TASK-014 — DXOS_MODE=demo

- **Depends:** TASK-013
- **AC:** Default env demo; no OAuth client id required. Document in SETUP.

## TASK-020 — IPlatformConnector + Capability enum

- **Depends:** TASK-011
- **Scope:** Domain or Application abstractions only. Capabilities enum. No SDK.
- **AC:** ArchUnit: interface not in Infrastructure-only if Domain-owned — put interface in `DXOS.Application.Abstractions` or `DXOS.Domain` per ADR (prefer Application.Abstractions to keep Domain pure).
- **Tests:** capabilities are additive flags.

## TASK-021 — platform_connections table + token vault

- **Depends:** TASK-012, TASK-020
- **Scope:** Table + encrypted payload (data protection / envelope). Never log token. API never returns raw token.
- **AC:** Integration test stores token, read back decrypts in Infrastructure only; Application DTO has no AccessToken field.

## TASK-022 — webhook_events idempotency

- **Depends:** TASK-012
- **Scope:** unique (org, provider, external_event_id); status machine received→processed|failed|duplicate
- **AC:** inserting same event twice returns duplicate, no second side effect (tested with fake handler).

## TASK-023 — Mock Facebook connector

- **Depends:** TASK-020, TASK-014
- **Scope:** Implements IPlatformConnector. Capabilities: READ_LEADS, READ_MESSAGES, READ_CAMPAIGN, READ_AD_INSIGHTS, WEBHOOK. Emits synthetic NormalizedEvent (may stub type if TASK-024 not merged — **if blocked, wait TASK-024**).
- **Order:** TASK-024 first if NormalizedEvent type not yet there. Graph: 020 → 024 → 023.
- **AC:** No network. Deterministic fixture. WireMock not required yet.

## TASK-024 — NormalizedEvent

- **Depends:** TASK-020
- **Scope:** Canonical event: provider, type, org, occurred_at, external_event_id, payload (typed lead/message/metric), source.
- **AC:** Unit tests round-trip JSON. No Facebook field names in Domain type.

## TASK-025 — Mock TikTok connector

- **Depends:** TASK-024
- **Scope:** Subset capabilities (declare honestly). Same NormalizedEvent.
- **AC:** Domain test cannot tell FB vs TT except provider string.

## TASK-026 — Mock Zalo connector

- **Depends:** TASK-024
- **AC:** Same as TikTok. Capabilities for OA messages/users/webhook as declared.

## TASK-027 — Pancake adapter (feature flag)

- **Depends:** TASK-024, existing `/api/sync`
- **Scope:** Map current batch payload → NormalizedEvent. Flag `DXOS_INGEST_PANCAKE=false` default.
- **Non-scope:** Changing extension selectors.
- **AC:** Existing `/api/sync` contract unchanged when flag off.

## TASK-030 — Lead + LeadSource entities

- **Depends:** TASK-012, TASK-024
- **AC:** EF migration; unique source+external id per org; FK customer optional until identity.

## TASK-031 — Lead intake application service

- **Depends:** TASK-022, TASK-030
- **AC:** Event → lead insert; duplicate event no second lead.

## TASK-032 — Identity resolution

- **Depends:** TASK-030
- **AC:** Same phone/email within org merges identities; different org no merge. Privacy: empty phone does not merge.

## TASK-033 — Scoring (rules first)

- **Depends:** TASK-031
- **AC:** 0–100, band HOT/WARM/COLD/SPAM, persist reason/model=`rules`/version/timestamp. Fixture: demo Facebook lead scores 87 HOT. AI optional later, cannot override without snapshot.

## TASK-034 — Routing + SLA

- **Depends:** TASK-033
- **AC:** HOT → assign round-robin sales + SLA clock; WARM queue; COLD nurture; SPAM archive. Audit log row.

## TASK-035 — Conversion

- **Depends:** TASK-034
- **AC:** Mark converted + revenue amount; campaign metric can read it.

## TASK-036 — Lead HTTP API

- **Depends:** TASK-034
- **Scope:** search/get/assign (assign goes through domain). Pagination. Tenant header.
- **AC:** Integration tests. No N+1 (assert query count or include).

## TASK-037 — Campaign metrics model + analytics query

- **Depends:** TASK-012
- **AC:** spend/impressions/clicks/leads/conversions/revenue + fetched_at/source/data_freshness. Compare by provider.

## TASK-038 — Judge demo seed

- **Depends:** TASK-025, TASK-026, TASK-034, TASK-037
- **Scope:** `scripts/seed-demo.ps1` + `scripts/reset-demo.ps1`. One customer 3 identities. FB lead 87 HOT assigned. Metrics for 3 platforms.
- **AC:** After seed, API analytics_summary returns 3 platforms. No real credentials.

## TASK-039 — DXOS.Web lead + analytics screens

- **Depends:** TASK-036, TASK-038, Taste skill
- **Non-scope:** Deleting admin-ui
- **AC:** Playwright later (TASK-081). Until then: HTTP + screenshot optional. UI not generic purple gradient slop.

## TASK-040 — Microsoft.Extensions.AI IChatClient

- **Depends:** TASK-011
- **Scope:** Application depends on `IChatClient` only. Infrastructure registers mock client in demo.
- **AC:** ArchUnit: Domain has no AI package. Demo works with mock, no API key.

## TASK-041 — Intent assist (non-authoritative)

- **Depends:** TASK-033, TASK-040
- **AC:** Extra snapshot model=`mock-intent`; rules score remains source of routing unless spec says otherwise (it does not — rules win for MVP).

## TASK-042 — Conversation summarization

- **Depends:** TASK-040
- **AC:** Summary stored; not a substitute for raw messages.

## TASK-050 — DXOS.Mcp façade

- **Depends:** TASK-036, TASK-037
- **Scope:** Tools call Application. Auth tenant. Audit. No tokens.
- **AC:** lead_search, lead_get, lead_assign, analytics_summary, platform_connections, sync_status. Old Node MCP untouched.

## TASK-060 — Facebook official OAuth (AFTER MVP)

- **Depends:** TASK-038 approved by Codex
- **Status until then:** NOT STARTED. Do not implement Graph API from memory. Context7 + official docs required.
- **AC:** capabilities from granted scopes only.

## TASK-061 / TASK-062 — TikTok / Zalo official

- Same gate as TASK-060.

## TASK-070 — Approval entities

- **Depends:** TASK-012
- **AC:** snapshot + version; content change requires re-approval (unit).

## TASK-071 — Elsa host for approval + lead SLA timeout

- **Depends:** TASK-006, TASK-034, TASK-070, Context7 Elsa docs
- **AC:** Human approval activity; workflow version recorded; CRUD still not in Elsa.

## TASK-080 — SBOM + scanners in CI

- **Depends:** TASK-010
- **AC:** gitleaks, trivy fs, syft cyclonedx to artifacts/; action fails on leaked JWT like existing Node secret job.

## TASK-081 — Playwright demo verifier

- **Depends:** TASK-039, TASK-038
- **AC:** `scripts/check-e2e.ps1` walks judge path.

## TASK-082 — Judge docs

- **Depends:** TASK-038
- **Scope:** `docs/demo/JUDGE_SCRIPT.md`, architecture one-pager, OPEN_SOURCE already exists.

---

# PART M — Dependency graph

```
TASK-000 ADR
   ├── TASK-001 skills
   │      ├── TASK-002 rules
   │      │      ├── TASK-003 openspec
   │      │      │      └── TASK-004 beads
   │      │      └── TASK-006 dxos skills
   │      └── TASK-005 OSS stubs
   └── TASK-010 .NET sln
          ├── TASK-007 check.ps1 (completes with sln)
          ├── TASK-011 ArchUnit
          │      ├── TASK-020 connector iface
          │      │      └── TASK-024 NormalizedEvent
          │      │             ├── TASK-023 mock FB
          │      │             ├── TASK-025 mock TT
          │      │             ├── TASK-026 mock Zalo
          │      │             └── TASK-027 pancake adapter (flag)
          │      └── TASK-040 IChatClient
          ├── TASK-012 tenant/org
          │      ├── TASK-013 Aspire+compose
          │      │      └── TASK-014 demo mode
          │      ├── TASK-021 connections+vault
          │      ├── TASK-022 webhook idempotency
          │      ├── TASK-030 leads
          │      │      └── TASK-031 intake ← 022
          │      │             └── TASK-032 identity
          │      │             └── TASK-033 scoring
          │      │                    └── TASK-034 routing
          │      │                           ├── TASK-035 conversion
          │      │                           └── TASK-036 API
          │      ├── TASK-037 analytics
          │      └── TASK-070 approvals
          └── TASK-080 scanners

TASK-023 + 025 + 026 + 034 + 037 → TASK-038 seed
TASK-036 + 038 → TASK-039 web → TASK-081 e2e
TASK-036 + 037 → TASK-050 MCP
TASK-038 Codex-approved → TASK-060 official FB (P2)
TASK-034 + 070 + 006 → TASK-071 Elsa
TASK-038 → TASK-082 judge docs
```

**First Gemini session:** TASK-000 only.  
**bd ready after 000–006:** TASK-010.

---

# PART N — Milestone plan

| Milestone | Tasks | Exit |
|---|---|---|
| M0 Agent brain | 000–007 | clone gives rules/skills/openspec/beads |
| M1 .NET OS boots | 010–014 | `dotnet build`, Aspire health, AIECOS compose still up |
| M2 Connectors mock | 020–027 | NormalizedEvent from 3 mocks + pancake flag |
| M3 Lead E2E | 030–038 | Judge seed: 87 HOT assign + 3-platform analytics |
| M4 Human surfaces | 039, 050, 081 | Web + MCP query |
| M5 AI assist | 040–042 | Non-authoritative |
| M6 Automation+OSS | 070–071, 080, 082 | Elsa approval, SBOM |
| M7 Official APIs | 060–062 | Only with real docs + scopes |

Do not start M7 to look impressive. M3 is the competition backbone.

---

# PART O — Gemini implementation prompts

## Universal contract (prepend to every task)

```text
ROLE: Senior implementer for DX-OS. You do not own spec or architecture.

SOURCE OF TRUTH ORDER:
1) Accepted OpenSpec  2) docs/adr  3) docs/architecture/DXOS_MASTER_PLAN.md
4) Beads AC  5) .agents/rules  6) .agents/skills  7) existing code  8) your assumptions
Conflict → STOP and report.

BEFORE CODING:
- Read this task fully.
- Inspect listed files.
- Use Serena/Graphify if available for symbols; do not grep the whole repo blindly.
- Use Context7 for Elsa / Aspire / EF / Microsoft.Extensions.AI versions actually referenced.
- Do not modify unrelated modules.

DO NOT:
- Rewrite AIECOS (chrome-extension, sync-receiver ingest contract, admin-ui framework).
- Replace Postgres, invent a new framework, add NuGet/npm without documenting in THIRD_PARTY_NOTICES and Beads note.
- Bypass IPlatformConnector / Domain rules.
- Disable tests or scanners.
- Commit .env or secrets.
- Approve your own work.
- Start official Facebook/TikTok/Zalo API until TASK-038 is Codex-approved.

DONE means: spec AC + tests + commands + evidence (diff, test output). Not a narrative.

FINAL RESPONSE must include:
- summary
- files changed
- tests added
- commands executed
- test results (paste)
- known limitations
- follow-up task id
- Beads status if bd exists
```

## Global tools (human machine; do not vendor binaries)

```text
# CLIs (global)
npm install -g @fission-ai/openspec@latest
npm install -g @beads/bd
# Graphify: https://github.com/Graphify-Labs/graphify
#   graphify antigravity install
# Serena MCP: https://github.com/oraios/serena
# Context7: https://github.com/upstash/context7
#   npx ctx7 setup
# Aspire: https://learn.microsoft.com/dotnet/aspire
# Gitleaks, Trivy, Syft, Grype, Docker, Git, .NET 10 SDK
```

Project-local (git): `.agents/`, `openspec/`, `.beads/`, `src/`, `tests/`, `scripts/`, `docs/`.

---

### GEMINI PROMPT — TASK-000

```text
TASK ID: TASK-000
PREPEND: universal contract.

OBJECTIVE: Record dual-stack ADR so nobody deletes AIECOS or rewrites it in .NET in one PR.

SCOPE: Create docs/adr/0001-dual-stack-aiecos-dotnet.md. Add a short README section "Relationship to AIECOS" that AIECOS is foundation ingest (Pancake/DOM), DX-OS product OS will live under src/ as .NET 10, AIECOS folders stay.

NON-SCOPE: C#, schema, UI, deleting files.

FILES TO INSPECT:
- README.md
- docker-compose.yml
- sync-receiver/schema.sql
- docs/ARCHITECTURE.md
- docs/architecture/DXOS_MASTER_PLAN.md (this file)

FILES EXPECTED TO CHANGE:
- docs/adr/0001-dual-stack-aiecos-dotnet.md (new)
- README.md (additive paragraph only)

IMPLEMENTATION:
- ADR must list KEEP: chrome-extension, sync-receiver, admin-ui, mcp-server, schema aiecos_social, docker-compose services.
- ADR must list NEW: src/DXOS.*, tests, Elsa, Aspire, OpenSpec, Beads, .agents.
- ADR must list FORBIDDEN: dropping Node to “clean the repo”; facebook_users tables; MCP scraping platforms.

ACCEPTANCE:
1. ADR file exists and states dual-stack.
2. docker compose config still works.
3. No runtime file changes.

VALIDATION:
docker compose -f docker-compose.yml config
git diff --stat

FOLLOW-UP: TASK-001
```

### GEMINI PROMPT — TASK-001

```text
TASK ID: TASK-001
PREPEND: universal contract.

OBJECTIVE: Vendor a SMALL selected skill set into .agents/skills with commit SHAs for Gemini/Antigravity.

SCOPE: Clone upstream, copy listed skill directories, write docs/THIRD_PARTY_SKILLS.md (url, sha, license, date).

NON-SCOPE: Writing dxos-* skills. Installing all 24 Addy skills. reverse-skill. Global MCP.

IMPLEMENTATION (Windows PowerShell OK):
1. mkdir .agents/skills, vendor/_tmp (vendor/_tmp gitignored)
2. git clone --depth 1 https://github.com/addyosmani/agent-skills.git vendor/_tmp/agent-skills
   Record SHA: git -C vendor/_tmp/agent-skills rev-parse HEAD
   Copy ONLY skills/<name>/ for:
   incremental-implementation, test-driven-development, context-engineering,
   source-driven-development, doubt-driven-development, api-and-interface-design,
   debugging-and-error-recovery, code-review-and-quality, code-simplification,
   security-and-hardening, git-workflow-and-versioning, ci-cd-and-automation,
   documentation-and-adrs, observability-and-instrumentation
   SKIP spec-driven-development and planning-and-task-breakdown.
3. git clone --depth 1 https://github.com/Leonxlnx/taste-skill.git vendor/_tmp/taste-skill
   Copy design-taste-frontend (path as in that repo: skills/taste-skill or documented --skill design-taste-frontend).
   If v2 vs v1: prefer design-taste-frontend; pin SHA; note experimental in notices.
4. git clone --depth 1 https://github.com/UnitOneAI/SecuritySkills.git vendor/_tmp/SecuritySkills
   Copy existing dirs only:
   skills/appsec/threat-modeling, secure-code-review, api-security, dependency-scanning
   skills/ai-security/llm-top-10, agentic-top-10, prompt-injection, ai-data-privacy
   skills/identity/rbac-design
   skills/cloud/container-security
   If secrets-management or pipeline-security exist, copy; else list as NOT FOUND — do not invent.
5. Flatten or keep names unique under .agents/skills/<skill-name>/SKILL.md so Antigravity discovers them.
6. Delete vendor/_tmp clones (do not commit entire upstream repos).
7. Ensure .gitignore does NOT ignore .agents/skills.

ALTERNATE if `npx skills` works for Antigravity project dir, still must pin SHAs in notices.

ACCEPTANCE:
- Each selected skill has SKILL.md
- Notices include SHAs
- No IDA/Frida/malware skills
- git status shows only intended paths

VALIDATION:
Get-ChildItem -Recurse .agents/skills -Filter SKILL.md | Select-Object FullName
```

### GEMINI PROMPT — TASK-002

```text
TASK ID: TASK-002
PREPEND: universal contract.
OBJECTIVE: Workspace rules with precedence and architecture/testing/security/ai/git.
SCOPE: .agents/rules/00-authority.md 10-dotnet-architecture.md 20-testing.md 30-security.md 40-database.md 50-ai-governance.md 60-git.md
NON-SCOPE: product code
INSPECT: DXOS_MASTER_PLAN.md parts D,H,I,J and TASK-002 section.
IMPLEMENTATION: 00-authority must equal the numbered source-of-truth list. 10-dotnet: Domain ↛ Infra, no platform SDK in Domain, Elsa not for CRUD. 20-testing: Testcontainers for persistence, ArchUnit required. 30-security: no secrets in git, tenant isolation, scanners. 40-database: org_id, FKs, idempotency unique. 50-ai: IChatClient; AI may classify/summarize/recommend; may NOT delete/spend/publish/cross-tenant. 60-git: no .env, conventional commits, no force-push main.
AC: files exist, no contradiction with master plan.
FOLLOW-UP: TASK-003
```

### GEMINI PROMPT — TASK-003

```text
TASK ID: TASK-003
OBJECTIVE: openspec init + change dxos-foundation proposal/design only.
SOURCE: https://github.com/Fission-AI/OpenSpec
COMMANDS: npm install -g @fission-ai/openspec@latest ; openspec init
NON-SCOPE: applying the change (no src yet)
AC: openspec/changes/dxos-foundation/{proposal.md,design.md,specs,tasks.md} describes dual-stack and MVP E2E; tasks.md points at Beads ids not a 500-line TODO.
FOLLOW-UP: TASK-004
```

### GEMINI PROMPT — TASK-004

```text
TASK ID: TASK-004
OBJECTIVE: Beads graph for TASK-000..082 with blocks matching PART M.
SOURCE: npm i -g @beads/bd ; https://github.com/gastownhall/beads
COMMANDS: bd init ; bd setup --list ; setup gemini or document if missing ; create issues with deps.
AC: bd ready --json after closing 000-006 does not include official OAuth tasks. .beads committed per upstream docs.
NON-SCOPE: changing AC of tasks
FOLLOW-UP: TASK-005
```

### GEMINI PROMPT — TASK-005 / 006 / 007

Use universal contract + PART L text as the spec. Do not invent extra markdown books.

TASK-006 skill bodies must stay short (entrypoint + links to references/). Include forbidden AI actions verbatim from 50-ai-governance.

TASK-007: `scripts/check.ps1` should:
- run existing node --check if sln missing (warn)
- after TASK-010: restore, format verify, build -warnaserror, test Architecture+Unit+Integration, gitleaks/trivy/syft if installed else warn
Do not fail AIECOS workflow jobs. Add a new job file `.github/workflows/dxos.yml` that only runs when src/** changes, initially skip until sln exists.

---

### GEMINI PROMPT — TASK-010 (first product code)

```text
TASK ID: TASK-010
PREPEND: universal contract.
OBJECTIVE: Empty .NET 10 modular monolith that builds.

INSPECT: Directory layout in PART D; do not delete Node projects.

CREATE:
- global.json (SDK 10.x — use Context7 / dotnet --list-sdks; if 10 not installed, STOP and report, do not silently drop to net8 unless owner confirms)
- DXOS.sln
- Directory.Build.props, Directory.Packages.props, .editorconfig
- src/DXOS.Domain, Application, Infrastructure, Api, Workflows, AppHost, Web
- tests/DXOS.Architecture.Tests, Unit.Tests, Integration.Tests, E2E.Tests (E2E may be empty placeholder)

PACKAGES (allowed to start):
- Microsoft.NET.Sdk
- Microsoft.AspNetCore.App
- xunit, Microsoft.NET.Test.Sdk
- ArchUnitNET.xUnit (or current ArchUnitNET test adapter — Context7)
- Aspire.Hosting.AppHost on AppHost
- EF Core Postgres only when TASK-012 — do not add in 010 if not needed to compile empty projects
No MediatR/AutoMapper/Hangfire/ABP.

AC:
1. dotnet build DXOS.sln -c Release
2. AIECOS folders untouched
3. THIRD_PARTY_NOTICES lists new NuGet

VALIDATION:
dotnet build DXOS.sln -c Release
git diff --stat

FOLLOW-UP: TASK-011
```

For TASK-011 through TASK-082: **the PART L block is the implementation spec.** Gemini must copy Objective/Scope/Non-scope/AC/Tests/Validation from PART L and prepend the universal contract. If a task is larger than one session, split and file Beads children — do not silently enlarge.

---

# PART P — Review protocol

After Gemini reports:

1. Codex reviews **git diff**, test log, build log, scanner output — not the story.
2. Rubric A–L (correctness, architecture, security, integrity, tenant, platform API honesty, reliability, performance, tests, maintainability, OSS, competition value).
3. Output:

```
TASK: TASK-XXX
VERDICT: APPROVED | CHANGES_REQUIRED
SEVERITY: BLOCKER|HIGH|MEDIUM|LOW
FINDINGS: [BLOCKER] [HIGH] [MEDIUM] [LOW]
ACCEPTANCE STATUS: [PASS]/[FAIL]
REQUIRED CHANGES: ...
OPTIONAL IMPROVEMENTS: ...
```

No approve with BLOCKER or failed AC. No next task until APPROVED.

---

# PART Q — Definition of Done

Implementation ≠ Done.

Done =

- OpenSpec/Beads AC satisfied
- Architecture tests pass
- Release build pass
- Unit + integration pass
- Security scan pass (or explicit waiver in Beads, never silent)
- Codex review of diff pass
- AIECOS compose still boots if the task did not intend to change it
- Docs updated if behavior changed
- No new secret, no extra NuGet without notice

---

# PART R — Top 20 architectural risks

1. **Rewriting AIECOS into .NET before E2E** — lose the only running demo. Mitigation: ADR-0001 + TASK freeze.
2. **Official API claims without docs** — hallucination. Mitigation: Context7 + NOT AVAILABLE status.
3. **Platform if-else in Domain** — Mitigation: ArchUnit + IPlatformConnector.
4. **MCP copies AIECOS raw SQL/REST** — Mitigation: TASK-050 façade only.
5. **Demo requires Facebook credentials** — fails OSS clone. Mitigation: mock default.
6. **Customer id = page+name** leak into DX-OS — Mitigation: new UUID customers + identities.
7. **No tenant filter** — cross-org leak. Mitigation: TASK-012 tests.
8. **Token in logs/MCP** — Mitigation: vault + review rubric C.
9. **AI publishes/spends** — Mitigation: dxos-ai-safety + approvals.
10. **Elsa for CRUD** — unmaintainable. Mitigation: skill + review.
11. **Duplicate scoring in UI and server** (AIECOS pattern) — Mitigation: server snapshot is source of truth.
12. **Non-idempotent webhooks** — duplicate leads. Mitigation: TASK-022 unique key.
13. **NuGet explosion** — Mitigation: Directory.Packages.props + Codex package veto.
14. **Skill overload** — Gemini ignores rules. Mitigation: selected skills only.
15. **Two planners (OpenSpec vs Addy spec skill)** — Mitigation: do not vendor those two Addy skills.
16. **Postgres port 5433 local hack** — document or revert; don’t bake into DX-OS.
17. **admin-ui vs DXOS.Web split-brain** — Mitigation: README which UI is judge path after M4.
18. **No tests today** — regressions. Mitigation: tests from TASK-011 onward, never skip.
19. **Pancake scraping as “open connection” story for judges** — honest docs: legacy adapter, mocks + API/webhooks are the open connection story.
20. **Scope creep M7 official APIs** — steal time from 45 business points. Mitigation: M3 gate.

---

## Immediate next action

The DX-OS .NET OS, OpenSpec, Beads, `.agents/`, and quality-gate scripts are **already inherited** from `ROYCE-8425/open_source` (ADR-0008). Do not recreate an empty `src/` skeleton or re-run TASK-010 as a blank slate.

Public product remote: `https://github.com/ROYCE-8425/Marketing_social.git`.

Gemini should take the next unimplemented Beads/OpenSpec task against this combined tree and must not rewrite AIECOS.
