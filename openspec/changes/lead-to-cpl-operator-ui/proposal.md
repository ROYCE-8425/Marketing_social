## Why

DX-OS already has a Lead-to-CPL **JSON API** (campaign approval, form-lead intake, scoring, claim, CPL dashboard) but **no operator web UI**. Judges and owners cannot click the product; they must use curl. This change records what is already shipped, what Gemini will code next (one operator console), and what stays out of scope so implementation stays gradual.

## What Changes

- Document the shipped Lead-to-CPL API as accepted slice behavior (not a new backend rewrite).
- Add a **minimal operator console** (HTML/CSS/JS served by DXOS.Api) so Marketer / Owner / Sales can run the same slice in a browser.
- Keep `X-DXOS-Role` and `X-DXOS-Actor` as the bootstrap actor model (no SSO yet).
- Do **not** add live ads publish, Zalo OA inbox, SSO, P.A.R.A wiki, auto-pause ads, or revenue/accounting.

## Capabilities

### New Capabilities

- `lead-to-cpl`: campaign approval state machine, form-lead intake, scoring, SLA claim, and CPL dashboard — API already present; UI is the remaining requirement.
- `operator-console`: browser UI for that slice only.

### Modified Capabilities

- None (bootstrap engineering specs are unchanged).

## Impact

- `src/DXOS.Api` (static pages + call existing marketing endpoints).
- Optional small read endpoints if the UI needs list-campaigns (today GET is by id only).
- No new NuGet packages unless a later task proves they are required.
- Bootstrap change `bootstrap-remediation-001` R7.1–R7.4 stay `[ ]`. R8 stays not started.
