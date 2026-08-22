[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  DX-OS Marketing — Judge Demo & Verification Script" -ForegroundColor Cyan
Write-Host "  Target: $BaseUrl" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$headersMarketer = @{
    "Content-Type" = "application/json"
    "X-DXOS-Role" = "Marketer"
    "X-DXOS-Actor" = "mai"
}

$headersSales = @{
    "Content-Type" = "application/json"
    "X-DXOS-Role" = "Sales"
    "X-DXOS-Actor" = "sales-1"
}

# 1. Health check
Write-Host "`n[1/6] Checking API Liveness & Readiness..." -ForegroundColor Yellow
$live = Invoke-RestMethod -Uri "$BaseUrl/health/live" -Method Get
Write-Host "  /health/live status: $($live.status)" -ForegroundColor Green

# 2. Seed baseline demo
Write-Host "`n[2/6] Seeding baseline demo data..." -ForegroundColor Yellow
$seedRes = Invoke-RestMethod -Uri "$BaseUrl/demo/seed" -Method Post -Headers $headersMarketer
Write-Host "  Seeded $($seedRes.campaignCount) campaigns and $($seedRes.leadCount) leads." -ForegroundColor Green

# 3. Ingest Facebook mock webhook lead (Scores 87 HOT)
Write-Host "`n[3/6] Ingesting Facebook Webhook Lead (Mock FB)..." -ForegroundColor Yellow
$fbPayload = @{
    eventId = "fb-judge-$(Get-Random)"
    name = "Nguyen Van Judge"
    phone = "0909888999"
    email = "judge@dxos.marketing"
    campaignId = $seedRes.campaignId
    notes = "Judge evaluation lead for demo verification"
} | ConvertTo-Json

$fbRes = Invoke-RestMethod -Uri "$BaseUrl/webhooks/facebook/leads" -Method Post -Headers $headersMarketer -Body $fbPayload
Write-Host "  Ingested Lead: $($fbRes.lead.name) | Score: $($fbRes.lead.score)/100 ($($fbRes.lead.label)) | Assigned: $($fbRes.lead.assignedToActor)" -ForegroundColor Green

# 4. Convert lead (Sales role with revenue)
Write-Host "`n[4/6] Converting Lead as Sales role with 15,000,000 VND revenue..." -ForegroundColor Yellow
$convertPayload = @{
    revenueVnd = 15000000
} | ConvertTo-Json

$convRes = Invoke-RestMethod -Uri "$BaseUrl/leads/$($fbRes.lead.id)/convert" -Method Post -Headers $headersSales -Body $convertPayload
Write-Host "  Lead Converted: $($convRes.isConverted) | Revenue: $($convRes.conversionRevenueVnd) VND" -ForegroundColor Green

# 5. Query Analytics by Platform
Write-Host "`n[5/6] Querying GET /analytics/leads-by-platform..." -ForegroundColor Yellow
$analytics = Invoke-RestMethod -Uri "$BaseUrl/analytics/leads-by-platform" -Method Get -Headers $headersMarketer
Write-Host "  Platform Analytics Summary:" -ForegroundColor Cyan
foreach ($p in $analytics.platforms) {
    Write-Host "    • $($p.provider.ToUpper()): Total=$($p.leadCount), Hot=$($p.hotCount), Warm=$($p.warmCount), Cold=$($p.coldCount), Converted=$($p.convertedCount), Revenue=$($p.revenueVnd) VND"
}

# 6. Query MCP Server via JSON-RPC
Write-Host "`n[6/6] Testing MCP JSON-RPC protocol (POST /mcp)..." -ForegroundColor Yellow
$mcpCallPayload = @{
    jsonrpc = "2.0"
    id = "judge-1"
    method = "tools/call"
    params = @{
        name = "analytics_summary"
        arguments = @{}
    }
} | ConvertTo-Json -Depth 5

$mcpRes = Invoke-RestMethod -Uri "$BaseUrl/mcp" -Method Post -Headers $headersSales -Body $mcpCallPayload
Write-Host "  MCP JSON-RPC response received successfully: isError=$($mcpRes.result.isError)" -ForegroundColor Green

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "  Demo Verification Finished Successfully!" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
