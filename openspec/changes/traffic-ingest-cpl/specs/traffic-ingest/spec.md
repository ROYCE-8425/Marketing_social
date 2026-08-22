## Purpose

Operators record campaign traffic (impressions, clicks, visits, spend in VND) so DX-OS can aggregate lưu lượng and feed Lead-to-CPL without a live ads platform.

## ADDED Requirements

### Requirement: Traffic snapshots are stored per campaign
A Marketer, Owner, or System actor SHALL record a traffic snapshot against an existing campaign. Each snapshot MUST include a UTC calendar date, non-negative impressions, clicks, visits, and spend in VND. The source MUST be `Manual` until an ads connector exists. Sales MUST NOT record traffic.

#### Scenario: Marketer records a day of traffic
- **WHEN** a Marketer posts impressions 1000, clicks 50, visits 40, spend 2_000_000 VND for a known campaign date
- **THEN** the snapshot is stored with `source=Manual` and `adsLive` remains false

#### Scenario: Sales cannot record traffic
- **WHEN** a Sales actor posts a traffic snapshot
- **THEN** the request is rejected with a forbidden-role error and no row is stored

#### Scenario: Missing campaign is rejected
- **WHEN** a snapshot is posted for a campaign id that does not exist
- **THEN** the request is rejected as not found

### Requirement: Snapshots aggregate into campaign totals
DX-OS SHALL expose per-campaign totals as the sum of stored snapshots: impressions, clicks, visits, spend VND. CTR MUST be clicks / impressions when impressions > 0, otherwise 0. Totals MUST NOT invent traffic from a live ads API.

#### Scenario: Two days sum
- **WHEN** a campaign has snapshots spend 1_000_000 and 500_000 VND with impressions 100 and 50
- **THEN** totals are spend 1_500_000 VND and impressions 150

### Requirement: Ingest runs as a product workflow
Recording a snapshot SHALL execute a bounded product workflow (not the engineering smoke workflow). The workflow MUST persist the snapshot and return the updated campaign totals. A failed workflow MUST NOT leave a silently half-applied operator-visible total.

#### Scenario: Successful ingest
- **WHEN** a valid snapshot is posted
- **THEN** the response includes the new snapshot and the campaign totals after aggregation

### Requirement: Negative or inverted metrics are rejected
Impressions, clicks, visits, and spend MUST be >= 0. Clicks MUST NOT exceed impressions. Spend MUST be VND (integer dong after rounding away from zero if a fraction is supplied).

#### Scenario: Clicks exceed impressions
- **WHEN** a snapshot has impressions 10 and clicks 11
- **THEN** the request is rejected and nothing is stored
