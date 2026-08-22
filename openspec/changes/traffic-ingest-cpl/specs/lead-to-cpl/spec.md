## MODIFIED Requirements

### Requirement: CPL dashboard is spend-in, leads-out
The dashboard SHALL compute CPL from current lead count and a spend amount in VND. Spend MUST come from stored traffic totals when the operator omits the `spend` query value. An explicit `spend` query MAY override the stored total for a one-off calculation and MUST NOT rewrite stored snapshots. The dashboard MUST report `adsLive=false` while no ads connector exists. Currency MUST be VND.

#### Scenario: Zero spend
- **WHEN** stored traffic spend is 0 and the operator does not pass `spend`
- **THEN** the dashboard returns lead count and CPL 0 with `adsLive=false` and `currency=VND`

#### Scenario: Stored traffic feeds CPL
- **WHEN** a campaign has stored traffic spend 2_000_000 VND, the operator requests CPL without `spend`, and there are 4 leads
- **THEN** the dashboard returns spend 2_000_000, leadCount 4, cpl 500_000, `adsLive=false`

#### Scenario: Query spend overrides display only
- **WHEN** stored spend is 2_000_000 and the operator passes `spend=0`
- **THEN** the dashboard uses 0 for that response and stored snapshots stay 2_000_000
