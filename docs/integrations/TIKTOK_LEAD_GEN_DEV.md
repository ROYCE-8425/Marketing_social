# TikTok Lead Generation (Official Marketing API) Integration Guide

**Status:** IMPLEMENTED & VERIFIED (Live & Mock Dual-Stack)  
**Target Platform:** TikTok for Business / TikTok Marketing API (Lead Generation)  
**API Version:** `v1.3`  

---

## 1. Architecture Overview

DX-OS connects directly to TikTok's official Marketing API and Webhook infrastructure without proprietary SDK dependencies:
- **Zero Domain Pollution**: Domain layer (`src/DXOS.Domain`) has zero knowledge of TikTok payload types.
- **Infrastructure Adapter**: `src/DXOS.Infrastructure/Integrations/TikTokLeadAdsClient.cs` handles HMAC-SHA256 signature verification (`X-TikTok-Signature` / `X-Signature`), payload extraction for standard & test lead formats, and multi-language key normalization using standard `HttpClient` and `System.Text.Json`.
- **Application Ingest**: Extends `LeadService.IntakePlatformWebhookAsync("tiktok", ...)` with idempotent deduplication via `webhook_events`.
- **Social CRM Persistence**: Ingests leads into `aiecos_social` schema (`pages`, `customers`, `conversations`, `messages`) and displays leads on the Social CRM Inbox (`/inbox/`) and Operator Board (`/board.html`).

```
TikTok Marketing API Webhook / Create Test Lead
       │ (POST /integrations/tiktok/webhook)
       ▼
MarketingEndpoints.cs ──(Verify X-TikTok-Signature)──► TikTokLeadAdsClient
       │
       ├─► Extract lead details (Name, Phone, Email, Form ID)
       │
       ├─► Ingest into aiecos_social (pages, customers, conversations, messages)
       │     └─► Realtime display on http://localhost:8080/inbox/
       │
       ▼
LeadService.IntakePlatformWebhookAsync("tiktok", ...)
       │
       ├─► Deduplicate lead_id via WebhookEventStore
       ├─► Score Lead (HOT/WARM/COLD) via LeadScoring
       └─► Persist Lead via LeadStore
```

---

## 2. Configuration & Environment Variables

Configure in local `.env` (gitignored, never committed):

```env
# TikTok for Business Marketing API Configuration
TIKTOK_APP_ID=<your-tiktok-app-id>
TIKTOK_APP_SECRET=<your-tiktok-app-secret>
TIKTOK_ADVERTISER_ID=<your-tiktok-advertiser-id>
TIKTOK_ACCESS_TOKEN=<your-tiktok-access-token>

# Mode: 'mock' (default fallback) or 'live'
TIKTOK_MODE=live

# Public HTTPS URL for Webhook Callback (via ngrok or cloudflared)
PUBLIC_BASE_URL=https://subepiglottal-rebeca-morally.ngrok-free.dev
```

---

## 3. Webhook Endpoints & Social CRM REST APIs

### 1) Verification Challenge (GET)
- **URL**: `{PUBLIC_BASE_URL}/integrations/tiktok/webhook`
- **Parameters**: `challenge` or `hub.challenge`
- **Response**: HTTP 200 with challenge text when present; HTTP 200 `{ status: "OK", provider: "tiktok" }` otherwise.

### 2) Webhook Event Ingest (POST)
- **URL**: `{PUBLIC_BASE_URL}/integrations/tiktok/webhook`
- **Security**: Verifies `X-TikTok-Signature` / `X-Signature` header (HMAC-SHA256 computed with `TIKTOK_APP_SECRET`).
- **Handles**:
  - `event == "leadgen"` or direct lead payload: Extracts form lead data, scores lead in DX-OS, and records in `aiecos_social`.

---

## 4. How to Test (Create a Test Lead)

You can fire a test lead using TikTok's official test lead format or the "Create a test lead" API without running a paid ad campaign:

```powershell
# Send a test lead payload to DX-OS
$payload = @{
    event = "leadgen"
    advertiser_id = "7123456789012345678"
    data = @{
        lead_id = "tt_lead_test_001"
        form_id = "form_tiktok_lead_01"
        field_data = @(
            @{ name = "full_name"; values = @("Trần Hoàng Nam (TikTok Lead)") },
            @{ name = "phone_number"; values = @("0988112233") },
            @{ name = "email"; values = @("hoangnam.tiktok@test.vn") }
        )
    }
} | ConvertTo-Json -Depth 5

Invoke-RestMethod -Uri "http://localhost:8080/integrations/tiktok/webhook" -Method Post -Body $payload -Headers @{
    "Content-Type" = "application/json"
}
```

Check the lead in DX-OS:
- Open `http://localhost:8080/inbox/` to view the TikTok lead conversation with green badge.
- Query `GET /leads` to see the scored lead (`source: TikTok`).
