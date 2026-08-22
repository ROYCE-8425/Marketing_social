## Why

Lead-to-CPL currently types spend into `/dashboard/cpl?spend=` and never stores traffic (impressions, clicks, visits, chi phí). Judges cannot see **lưu lượng → chi phí → lead → CPL** as one data path. This change persists operator-entered traffic against a campaign and folds those totals into the same CPL dashboard. It does not connect Facebook/TikTok/Google Ads.

## What Changes

- Persist **traffic snapshots** per campaign (date, impressions, clicks, visits, spend VND, source=`Manual`).
- **Aggregate** snapshots into campaign totals (sum spend, sum impressions/clicks, CTR).
- Run ingest through an **Elsa 3.7.1 product workflow** in `DXOS.Workflows` (not the engineering smoke workflow). HTTP records an input; the workflow writes the snapshot and returns the new totals.
- `GET /dashboard/cpl` uses **stored spend** when the query `spend` is omitted; `adsLive` stays `false`; currency stays VND.
- Operator console: Vietnamese panel **Lưu lượng** to enter traffic and see totals on the same page.
- Complete the already-started local automation in the working tree (send-to-owner, demo seed, message/call records, SLA remaining, spend pacing) so Gemini owns all remaining product code. Grok does not implement this change.

Non-goals (do not implement): live ads APIs, pulling spend from a platform, SSO, Zalo inbox, revenue/accounting, Elsa Studio, ticking `bootstrap-remediation-001` 7.1–7.4, R8.

## Capabilities

### New Capabilities

- `traffic-ingest`: manual traffic snapshots, campaign aggregation, Elsa ingest workflow, and CPL feed from stored spend.

### Modified Capabilities

- `lead-to-cpl`: CPL dashboard MUST prefer stored traffic spend when the operator does not pass `spend`; still `adsLive=false`.
- `operator-console`: add a Vietnamese traffic panel that posts snapshots and shows aggregated lưu lượng + CPL.

## Impact

- `DXOS.Domain` — traffic snapshot + aggregation rules (no Elsa types).
- `DXOS.Application` — traffic service + store port.
- `DXOS.Infrastructure` — EF entity, table, migration; AutoMigrate already exists.
- `DXOS.Workflows` — `TrafficIngestWorkflow` beside smoke; `EngineeringSmokeWorkflow` unchanged.
- `DXOS.Api` — `POST/GET` traffic routes; dashboard reads stored totals; `wwwroot/index.html` panel.
- Tests: unit for aggregation and poka-yoke; architecture rules still pass (Domain must not reference Elsa).
- Docs: `docs/PRODUCT_BOARD.md`, `docs/runtime.md` §3.1, OpenSpec board.
- Working tree already has uncommitted send-to-owner / seed / pacing files; Gemini finishes those in the same apply, then traffic.
