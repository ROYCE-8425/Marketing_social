## Purpose

Lead-to-CPL is the first product slice: DX-OS collects and scores marketing leads and publishes campaigns only after Marketer then Owner approval. DX-OS does not close deals or record revenue.

## ADDED Requirements

### Requirement: Campaign approval is a two-person poka-yoke
A campaign SHALL move `Draft → PendingReview → PendingApproval → Published` (or `Rejected`). System/AI MUST NOT approve. Owner MUST NOT skip Marketer review. Ads MUST NOT be pushed.

#### Scenario: Marketer creates a draft
- **WHEN** a Marketer posts a campaign topic
- **THEN** the campaign is stored as `Draft` with stub copy and `adsPushed=false`

#### Scenario: Owner cannot skip review
- **WHEN** an Owner tries to approve a campaign that is not `PendingApproval`
- **THEN** the API rejects the transition

#### Scenario: Owner publishes after review
- **WHEN** a campaign is `PendingApproval` and Owner approves
- **THEN** status becomes `Published` and `adsPushed` remains false

### Requirement: Form leads are scored and listable
Form leads SHALL be accepted with name plus optional phone/email, scored 80 (phone+email), 50 (one of them), or 20 (neither), and listed for Sales claim with a 15-minute unclaim SLA.

#### Scenario: Complete contact scores 80
- **WHEN** a form lead includes both phone and email
- **THEN** the stored score is 80

#### Scenario: Sales claims a lead
- **WHEN** a Sales actor claims an unclaimed lead
- **THEN** the lead records the claimer and claim time

### Requirement: CPL dashboard is spend-in, leads-out
The dashboard SHALL compute CPL from an operator-supplied spend amount and current lead count, and MUST report `adsLive=false` while no ads connector exists.

#### Scenario: Zero spend
- **WHEN** spend is 0
- **THEN** the dashboard returns lead count and CPL 0 with `adsLive=false`
