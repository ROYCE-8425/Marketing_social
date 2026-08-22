## Context

Lead-to-CPL API already exists in `MarketingEndpoints.cs` (`1a4d3be`). The operator page is `src/DXOS.Api/wwwroot/index.html`. Visual and language rules are in [docs/design.md](../../../../docs/design.md): Vietnamese UI, VND, light work-management layout (not an “AI console”). Compose serves the API on port 8080. Actor identity is headers, not login.

## Goals / Non-Goals

**Goals**

- One-page operator console, same origin as the API.
- Role switcher: Marketer / Owner / Sales (and optional System for webhook demo).
- Call existing JSON endpoints; show status and errors.
- Keep bootstrap health/smoke routes unchanged.

**Non-Goals**

- SSO, Elsa Studio, live ads APIs, Zalo/Telegram, P.A.R.A wiki, autonomous spend agents, revenue.
- New npm/React toolchain unless a later change accepts it. Prefer static HTML+CSS+vanilla JS under `src/DXOS.Api/wwwroot`.

## Decisions

1. **Static console, not a second host.** `app.UseDefaultFiles(); app.UseStaticFiles();` plus `wwwroot/index.html`.
2. **If list-campaigns is missing**, add `GET /campaigns` returning recent campaigns so the UI is usable. That is an allowed API gap-fill, not a new product.
3. **No LLM.** Draft copy stays the stub string from the API.
4. **Visual QA.** After UI exists, open `/` in a browser and click create → submit → approve → lead → CPL.

## Risks / Tradeoffs

- Header-based roles are demo-grade; documented as bootstrap until identity exists.
- In-memory vs PostgreSQL store depends on current `CampaignStore` implementation; UI must not assume a second database.
- GitHub CI Aspire smoke is a separate track; this change must not regress `/health/live`.

## Migration Plan

Ship UI behind existing Compose/API. No schema migration required unless list/query needs a new index (unlikely for spike).

## Open Questions

- None for the first UI wave. Login/SSO is a later change.
