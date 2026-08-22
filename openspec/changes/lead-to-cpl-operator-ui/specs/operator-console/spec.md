## Purpose

A browser operator console lets Marketer, Owner, and Sales run the Lead-to-CPL slice without curl. It is not a full marketing suite.

## ADDED Requirements

### Requirement: Console is a real web page
DX-OS SHALL serve at least one HTML page from the API host (same origin as `/campaigns` and `/leads`) with role/actor inputs and the four operator surfaces. Visible copy SHALL be Vietnamese. Money SHALL display as VND (`vi-VN`). The page SHALL follow `docs/design.md` (light work-management layout).

#### Scenario: Operator opens the console
- **WHEN** a browser GETs the documented console path (default `/`)
- **THEN** an HTML UI is returned (not JSON) with Vietnamese labels, VND amounts, campaign/lead/CPL sections, and a banner that the product is not release-ready

### Requirement: Marketer can create and submit a campaign
The UI SHALL create a Draft via `POST /campaigns` and advance review via `POST /campaigns/{id}/submit-review` using `X-DXOS-Role` and `X-DXOS-Actor`.

#### Scenario: Marketer submits twice
- **WHEN** the operator creates a campaign then clicks submit review until status is `PendingApproval`
- **THEN** the page shows the new status without requiring curl

### Requirement: Owner can approve or reject from the page
The UI SHALL call approve/reject endpoints and MUST display API errors (for example skip-review) instead of hiding them.

#### Scenario: Skip-review is visible
- **WHEN** Owner tries to approve a campaign that is still `PendingReview`
- **THEN** the UI shows the server error text

### Requirement: Sales can see leads and claim
The UI SHALL list leads and claim one, then refresh the table.

#### Scenario: Form lead appears
- **WHEN** a form lead is posted (console form or webhook)
- **THEN** the leads table shows name, score, and claim state

### Requirement: CPL panel is on the same page
The UI SHALL request `/dashboard/cpl` with a spend number the operator types.

#### Scenario: Operator types spend
- **WHEN** spend is submitted
- **THEN** the panel shows leadCount, cpl, and adsLive false
