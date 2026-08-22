# Lead-to-CPL board (visual)

Legend: `[x]` shipped · `[ ]` code next · `[~]` later, not this change

```text
NOW (API — shipped)          NEXT (UI — this change)         LATER (not this change)
─────────────────────        ────────────────────────        ────────────────────────
[x] Campaign draft           [x] Page at /                   [~] SSO / login
[x] Marketer submit-review   [x] Role switcher               [~] Live Facebook/TikTok ads
[x] Owner approve/reject     [x] Campaign list + actions     [~] Zalo OA / SMS inbox
[x] Poka-yoke skip-review    [x] Lead table + claim          [~] P.A.R.A wiki
[x] Form lead + score 80/50/20 [x] CPL panel + spend input   [~] Auto-pause ads / A/B
[x] Sales claim + 15m SLA    [x] GET /campaigns if missing   [~] Revenue / accounting
[x] Dashboard CPL JSON       [x] Browser click-path QA       [~] Full marketing UI kit
[x] Health + Elsa smoke
```

Do not tick `bootstrap-remediation-001` tasks 7.1–7.4 here.

## 1. API already shipped (do not rebuild)

- [x] 1.1 Domain campaign state machine and poka-yoke (`DXOS.Domain`)
- [x] 1.2 Lead scoring 80 / 50 / 20 and form intake
- [x] 1.3 Application services + header actor (`X-DXOS-Role`, `X-DXOS-Actor`)
- [x] 1.4 Marketing JSON endpoints in `MarketingEndpoints.cs`
- [x] 1.5 Unit tests for scoring and transitions; architecture tests still pass
- [x] 1.6 Health/live, health/ready, smoke/workflow preserved

## 2. Operator console (Gemini wave — code this)

- [x] 2.1 Add `wwwroot` static files on DXOS.Api (`UseDefaultFiles` + `UseStaticFiles`)
- [x] 2.2 One HTML page at `/` with banner `NOT_READY` and role/actor fields
- [x] 2.3 Campaign panel: create, submit-review, approve, reject, status display
- [x] 2.4 Add `GET /campaigns` if the UI cannot list drafts without it
- [x] 2.5 Leads panel: webhook/form create, table, claim button
- [x] 2.6 CPL panel: spend input, show leadCount / cpl / adsLive=false
- [x] 2.7 Show API error bodies (skip-review 409) in the page
- [x] 2.8 Click-path in browser against running API (Compose port 8080)
- [x] 2.9 Vietnamese copy, VND (₫), and docs/design.md work-management tokens (not AI-dark theme)

## 3. Out of scope (leave `[~]`, do not implement in this change)

- [~] 3.1 SSO / cookie login
- [~] 3.2 Publish to Facebook / TikTok / Google Ads
- [~] 3.3 Zalo OA, SMS, Telegram 1-click approve
- [~] 3.4 Brand wiki P.A.R.A and Brand Guardian
- [~] 3.5 Autonomous spend allocator / auto-pause ads
- [~] 3.6 Elsa visual studio / separate SPA toolchain
