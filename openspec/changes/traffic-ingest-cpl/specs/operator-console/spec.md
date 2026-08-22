## ADDED Requirements

### Requirement: Console has a Vietnamese traffic panel
The operator page SHALL let Marketer or Owner enter impressions, clicks, visits, spend (₫), and a date for a selected campaign, then show campaign totals and the CPL panel from stored spend. Labels MUST be Vietnamese. The page MUST state that sàn quảng cáo is not connected.

#### Scenario: Operator records lưu lượng
- **WHEN** the operator submits a valid traffic row from the page
- **THEN** the page shows updated impressions, clicks, spend ₫, and refreshed CPL without using curl

#### Scenario: API error is visible
- **WHEN** Sales tries to record traffic from the page
- **THEN** the page shows the forbidden-role error text
