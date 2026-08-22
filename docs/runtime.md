# DX-OS Engineering Runtime Guide

This document describes the runtime spike architecture, development orchestration paths, health model, and verification workflows for DX-OS bootstrap milestone BR001-R4.

---

## 1. Runtime Architecture

DX-OS uses two independent runtime paths for developer workflows and containerized demo environments:

1. **.NET Aspire Developer Control Plane** (`src/DXOS.AppHost`):
   - Orchestrates local dependencies (`postgres:18.4-alpine`) and the API host (`src/DXOS.Api`).
   - Surfaces structured telemetry, logs, resources, and endpoints for local development.
   - Run via `dotnet run --project src/DXOS.AppHost/DXOS.AppHost.csproj`.

2. **Docker Compose Demo Path** (`compose.yaml`):
   - Standalone container deployment running PostgreSQL 18.4 and a multi-stage containerized `dxos-api` build.
   - Independent of Aspire; requires only Docker Engine & Docker Compose.
   - Run via `docker compose -f compose.yaml up -d --build`.

---

## 2. Persistence & Migrations

- **Database Engine**: PostgreSQL 18.4 (`postgres:18.4-alpine`).
- **Entity Framework Core**: DbContext lives in `DXOS.Infrastructure.Persistence.BootstrapDbContext`.
- **Engineering Checkpoints**: Minimal `runtime_probes` table tracking probe executions.
- **Migration Execution**:
  - Automatically applied on startup when `Database:AutoMigrate=true`.
  - Or manually applied using:
    ```powershell
    dotnet ef database update --project src/DXOS.Infrastructure/DXOS.Infrastructure.csproj --startup-project src/DXOS.Api/DXOS.Api.csproj
    ```

---

## 3. Endpoints & Health Model

| Endpoint | Method | Purpose | Behavior |
|---|---|---|---|
| `/health/live` | `GET` | Liveness Probe | Returns `200 OK` if the web host is alive; does **not** query PostgreSQL. |
| `/health/ready` | `GET` | Readiness Probe | Performs a real database connectivity check on PostgreSQL. Returns `200 OK` when ready, or `503 Service Unavailable` if unreachable. |
| `/smoke/workflow` | `POST` | Elsa Smoke Workflow | Executes the deterministic `EngineeringSmokeWorkflow` via Elsa 3 `IWorkflowRunner`. Returns `200 OK` with workflow instance ID, status `Completed`, and deterministic output `DXOS_SMOKE_OK`. Fails fast (`503/500`) if dependencies are unavailable. |

## 3.1 Lead-to-CPL slice (NOT_READY)

Temporary headers: `X-DXOS-Role` (`Owner` / `Marketer` / `Content` / `Sales` / `System`) and `X-DXOS-Actor`. No Identity/SSO. Campaign copy is a local stub from topic — no LLM and no API key. DX-OS stops at Lead/CPL; it does not close orders or push ads.

- `POST /campaigns` — create `Draft` (AI stub copy)
- `POST /campaigns/{id}/submit-review` — Marketer: `Draft` → `PendingReview` → `PendingApproval`
- `POST /campaigns/{id}/send-to-owner` — Marketer: `Draft` → `PendingApproval`
- `POST /campaigns/{id}/approve` — Owner only, `PendingApproval` → `Published` (does **not** push ads)
- `POST /campaigns/{id}/reject` — Owner or Marketer
- `GET /campaigns` — list campaigns
- `GET /campaigns/{id}` — get single campaign
- `POST /campaigns/{id}/traffic` — executes `TrafficIngestWorkflow` (Elsa 3) to record impressions, clicks, visits, spend
- `GET /campaigns/{id}/traffic` — list snapshots + aggregate totals (impressions, clicks, visits, spend, CTR)
- `POST /leads/webhook` — Form lead `{ name, phone, email, campaignId? }` (score 80)
- `POST /leads/message` — Message lead `{ name, phone, email, campaignId? }` (score 50)
- `POST /leads/call` — Call lead `{ name, phone, email, campaignId? }` (score 50)
- `GET /leads` — list leads with remaining SLA seconds
- `POST /leads/{id}/claim` — Sales claim
- `POST /demo/seed` — seed demo campaigns and leads
- `GET /dashboard/cpl?spend=&dailySpend=&budget=` — CPL dashboard using stored spend (or optional query override)

`NOT_READY`: no Facebook/TikTok/Google Ads live, no Zalo OA inbox, no A/B CTR, no revenue/accounting UI.

---

## 4. Runtime Smoke Automation

The script `scripts/smoke-runtime.ps1` executes automated end-to-end smoke verification for either runtime mode:

### Docker Compose Mode
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-runtime.ps1 -Mode Compose
```

### Aspire Mode
```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File scripts\smoke-runtime.ps1 -Mode Aspire
```

The script:
1. Validates the repository root.
2. Starts the target environment with bounded startup timeouts.
3. Verifies PostgreSQL readiness and API liveness/readiness.
4. Executes the Elsa workflow smoke and validates instance ID, `Completed` status, deterministic output, and correlation ID.
5. Tests negative dependency handling (database unavailability).
6. Tears down and cleans task-owned processes, containers, and resources fail-closed in `try ... finally`.
