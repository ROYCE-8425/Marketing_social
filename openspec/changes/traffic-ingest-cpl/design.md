## Context

See proposal.md for why. Lead-to-CPL already stores campaigns and leads in PostgreSQL (`campaigns`, `leads`) and computes CPL from a query `spend`. Spend is not persisted. Elsa 3.7.1 is NuGet-only; the only workflow today is `EngineeringSmokeWorkflow` at `POST /smoke/workflow`. Domain MUST NOT reference Elsa, EF, or the API. UI is `wwwroot/index.html` (Vietnamese, VND, `docs/design.md`).

Working tree (uncommitted, started earlier, **Gemini finishes** — Grok does not code this apply):

- `Campaign.SendToOwner`, `POST .../send-to-owner`
- `Lead.SlaRemainingSeconds`, `POST /leads/message`, `POST /leads/call`
- `DemoSeedService` + `POST /demo/seed`
- `SpendPacing` + CPL `dailySpend`/`budget`
- UI not wired to those routes yet

Repo: `C:\Users\199X\OneDrive\Máy tính\olympic\dx-os` (not the Elsa `open_source` checkout). Official remote `https://github.com/ROYCE-8425/open_source.git`.

## Goals / Non-Goals

**Goals:**

- One persisted traffic table, aggregated in Domain/Application, ingested through a product Elsa workflow.
- Same-origin console panel; dashboard reads stored spend.
- Keep smoke workflow, health routes, and architecture tests intact.

**Non-Goals:**

- Facebook/TikTok/Google Ads SDK, OAuth, or scheduled pull.
- Changing campaign poka-yoke or lead scoring.
- Elsa Studio, new SPA, SSO, Zalo, revenue.
- Grok implementing application code in this change.

## Decisions

1. **Traffic is a snapshot list, not a live meter.** `TrafficSnapshot` (id, campaignId, periodDate, impressions, clicks, visits, spendVnd, source=Manual, recordedByActor, createdAtUtc). Totals = SUM. Alternative rejected: mutating a single campaign row (loses history for judges).

2. **Elsa owns the ingest path, not the dashboard read.** `POST /campaigns/{id}/traffic` runs `TrafficIngestWorkflow` via `IWorkflowRunner` (same pattern as smoke, separate workflow id). The workflow calls `TrafficService` (Application). `GET` totals and `GET /dashboard/cpl` skip Elsa. Alternative rejected: persist in the endpoint then optionally run Elsa (two sources of truth). Alternative rejected: putting EF in `DXOS.Workflows` (architecture: Workflows → Application → Domain).

3. **Campaign must exist; Published is not required.** Manual demo needs traffic before a fake “ads live” state. Rejected only when campaign is missing or `Rejected`. Alternative (Published-only) would block the demo seed path.

4. **Roles.** Marketer, Owner, System may ingest (System reserved for a later connector with the same JSON). Sales and Content get `ForbiddenRole`. Headers stay `X-DXOS-Role` / `X-DXOS-Actor`.

5. **CPL spend resolution.** If query `spend` is present, use it for that response only. If absent, sum all snapshots (all campaigns) for the global dashboard that already exists — plus return `traffic` totals. Do not silently mix query spend with stored spend.

6. **Currency.** `spendVnd` is `decimal` stored as VND. Round to 0 decimal places, away from zero. UI `Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })`.

7. **Finish local automation first.** Gemini completes uncommitted send-to-owner / seed / SLA / pacing / UI wiring, then adds traffic on that baseline so one product board is true.

8. **No new NuGet** unless Elsa workflow APIs already referenced by smoke are insufficient.

## Risks / Trade-offs

- [Elsa workflow timeout / non-Finished] → Same 15s bound and fail-closed mapping as smoke; do not return 200 if persist did not complete.
- [Architecture leak: Domain references Elsa] → Keep snapshot types in Domain; workflow activities in `DXOS.Workflows`; ArchUnit tests must stay green.
- [Dashboard was global, traffic is per-campaign] → Keep global CPL as sum of all snapshots; also return per-campaign totals on GET traffic. Do not split the existing one-page CPL card into multi-campaign accounting.
- [Uncommitted local files conflict] → Gemini `git status` first; finish those files; do not revert unless they fail tests.
- [Looks like live ads] → Banner and JSON `adsLive=false`, `source=Manual` always in this change.

## Migration Plan

- Add EF entity + `OnModelCreating` table `traffic_snapshots`.
- Generate a migration in `DXOS.Infrastructure`; Compose/API already AutoMigrate when `Database:AutoMigrate=true`.
- Rollback: drop table / revert migration; dashboard `spend` query still works.

## Open Questions

- None that block apply. A later change may add `source=AdsConnector` without changing snapshot shape.
