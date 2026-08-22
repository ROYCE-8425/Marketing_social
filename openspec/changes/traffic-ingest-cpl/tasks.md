# Traffic ingest → CPL (visual)

Legend: `[x]` shipped · `[ ]` Gemini codes this change · `[~]` later, not this change

```text
NOW (uncommitted local)       THIS CHANGE (Gemini)              LATER
─────────────────────────     ────────────────────────────      ────────────────
[x] send-to-owner API/UI      [x] TrafficSnapshot persist       [~] Ads connector
[x] demo seed                 [x] Aggregate totals              [~] Facebook/TikTok pull
[x] message/call records      [x] Elsa TrafficIngestWorkflow    [~] SSO / Zalo
[x] SLA remaining + pacing    [x] POST/GET traffic              [~] Revenue
[x] wire those in index.html  [x] CPL uses stored spend         [~] Elsa Studio
                              [x] UI panel Lưu lượng (vi-VN)
```

Do not tick `bootstrap-remediation-001` tasks 7.1–7.4. R8 stays not started. Grok does not apply this change.

## 1. Finish uncommitted automation already in the tree

- [x] 1.1 Complete `SendToOwner`, `DemoSeedService`, message/call intake, `SlaRemainingSeconds`, `SpendPacing` so unit tests cover them (do not revert these files unless they fail compile)
- [x] 1.2 Wire `wwwroot/index.html` to send-to-owner, demo seed, SLA countdown, message/call, pacing (Vietnamese, VND)
- [x] 1.3 `dotnet test` unit + architecture projects green for that baseline before traffic tables

## 2. Domain and persistence

- [x] 2.1 Add traffic snapshot + aggregation rules in `DXOS.Domain` (non-negative metrics, clicks ≤ impressions, CTR, VND rounding) with no Elsa/EF types
- [x] 2.2 Add Application port + `TrafficService` (roles Marketer/Owner/System only)
- [x] 2.3 Add EF entity, `traffic_snapshots` table, store, and migration; AutoMigrate still applies it

## 3. Elsa ingest workflow

- [x] 3.1 Add `TrafficIngestWorkflow` in `DXOS.Workflows` that calls `TrafficService` and returns snapshot + totals
- [x] 3.2 Keep `EngineeringSmokeWorkflow` and `POST /smoke/workflow` unchanged
- [x] 3.3 Fail closed if the workflow is not Finished (same bound as smoke)

## 4. HTTP and dashboard

- [x] 4.1 `POST /campaigns/{id}/traffic` runs the ingest workflow; `GET /campaigns/{id}/traffic` lists snapshots + totals
- [x] 4.2 `GET /dashboard/cpl` uses stored spend when `spend` is omitted; explicit `spend` overrides display only; `adsLive=false`; `currency=VND`
- [x] 4.3 Register the workflow in `Program.cs` beside smoke

## 5. Operator console

- [x] 5.1 Vietnamese **Lưu lượng** panel: date, impressions, clicks, visits, spend ₫, campaign select, totals, error bodies
- [x] 5.2 Refresh CPL from stored spend after a successful ingest
- [x] 5.3 Banner still CHƯA SẴN SÀNG / chưa kết nối sàn quảng cáo

## 6. Tests and docs

- [x] 6.1 Unit tests: aggregation sum, clicks>impressions rejected, Sales forbidden, CPL from stored spend, query spend does not rewrite snapshots
- [x] 6.2 Architecture tests still pass (Domain must not reference Elsa)
- [x] 6.3 Update `docs/PRODUCT_BOARD.md` and `docs/runtime.md` §3.1 with the new routes; leave R7 checkboxes `[ ]`

## 7. Out of scope

- [~] 7.1 Live Facebook / TikTok / Google Ads pull
- [~] 7.2 SSO, Zalo OA, revenue, Elsa Studio
