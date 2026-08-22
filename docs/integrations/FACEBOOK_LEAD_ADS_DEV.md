# Facebook Lead Ads (Official Meta Graph API) Integration Guide

**Status:** IMPLEMENTED (Development Mode & Mock Dual-Stack)  
**Meta App Name:** `DX-OS Marketing Dev`  
**Meta App ID:** `2184057745525782`  
**API Version:** `v21.0`  

---

## 1. Architecture Overview

DX-OS connects directly to Meta's official Graph API and Webhook infrastructure without proprietary SDK dependencies:
- **Zero Domain Pollution**: Domain layer (`src/DXOS.Domain`) has zero knowledge of Facebook or Meta payload types.
- **Infrastructure Adapter**: `src/DXOS.Infrastructure/Integrations/FacebookLeadAdsClient.cs` handles HMAC-SHA256 signature verification and Graph API `GET /{leadgen_id}` requests using standard `HttpClient` and `System.Text.Json`.
- **Application Ingest**: Extends `LeadService.IntakePlatformWebhookAsync` with idempotent deduplication via `webhook_events`.

```
Meta Webhook / Testing Tool
       │ (POST /integrations/facebook/webhook)
       ▼
MarketingEndpoints.cs ──(Verify X-Hub-Signature-256)──► FacebookLeadAdsClient
       │
       ├─► (Fetch GET /v21.0/{leadgen_id} if Page Token present)
       │
       ▼
LeadService.IntakePlatformWebhookAsync
       │
       ├─► Deduplicate leadgen_id via WebhookEventStore
       ├─► Score Lead (HOT/WARM/COLD) via LeadScoring
       └─► Persist Lead via LeadStore
```

---

## 2. Configuration & Environment Variables

Configure in local `.env` (gitignored, never committed):

```env
# Meta Developer Configuration
FACEBOOK_APP_ID=2184057745525782
FACEBOOK_APP_SECRET=<your-app-secret>
FACEBOOK_PAGE_ID=<your-page-id>
FACEBOOK_PAGE_ACCESS_TOKEN=<your-page-access-token>
FACEBOOK_VERIFY_TOKEN=<your-verify-token>

# Mode: 'mock' (default fallback) or 'live' (calls Graph API)
FACEBOOK_MODE=mock

# Public HTTPS URL for Webhook Callback (via ngrok or cloudflared)
PUBLIC_BASE_URL=https://xxxx.ngrok-free.app
```

---

## 3. Webhook Endpoints

### 1) Verification Challenge (GET)
- **URL**: `{PUBLIC_BASE_URL}/integrations/facebook/webhook`
- **Parameters**: `hub.mode=subscribe`, `hub.verify_token=<token>`, `hub.challenge=<challenge>`
- **Response**: HTTP 200 with raw challenge text when tokens match; HTTP 401 when tokens mismatch.

### 2) Webhook Event Ingest (POST)
- **URL**: `{PUBLIC_BASE_URL}/integrations/facebook/webhook`
- **Security**: Verifies `X-Hub-Signature-256` header (HMAC SHA-256 computed with `FACEBOOK_APP_SECRET`).
- **Idempotency**: `leadgen_id` is tracked in `webhook_events`. Duplicate deliveries return `EVENT_RECEIVED` without creating duplicate leads.

---

## 4. How to Test via Meta Lead Ads Testing Tool

1. Start your local tunnel:
   ```powershell
   ngrok http 8080
   # Or when running direct: ngrok http 5000
   ```
2. In Meta App Dashboard (`https://developers.facebook.com/apps/2184057745525782/webhooks/`):
   - Set Callback URL to `{YOUR_NGROK_HTTPS_URL}/integrations/facebook/webhook`
   - Set Verify Token to `FACEBOOK_VERIFY_TOKEN` (default: `dxos_marketing_verify_token_2026`).
   - Subscribe to the `leadgen` field on Page object.
3. Open **Lead Ads Testing Tool**:
   - URL: `https://developers.facebook.com/tools/lead-ads-testing`
   - Select your Page and Lead Form.
   - Click **Create Lead** / **Tạo khách hàng tiềm năng**.
   - Click **Track Status** / **Theo dõi trạng thái** to see webhook delivery 100% success.
4. Check local DX-OS Operator Board at `/board.html` or query `GET /analytics/leads-by-platform` to see the newly ingested lead.
