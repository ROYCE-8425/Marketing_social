# Facebook Lead Ads & Messenger (Official Meta Graph API) Integration Guide

**Status:** IMPLEMENTED & VERIFIED (Live & Development Mode Dual-Stack)  
**Meta App Name:** `DX-OS Marketing Dev`  
**Meta App ID:** `2184057745525782`  
**Target Page:** `Royce Shop` (ID: `988656934325292`)  
**API Version:** `v21.0`  

---

## 1. Architecture Overview

DX-OS connects directly to Meta's official Graph API and Webhook infrastructure without proprietary SDK dependencies:
- **Zero Domain Pollution**: Domain layer (`src/DXOS.Domain`) has zero knowledge of Facebook or Meta payload types.
- **Infrastructure Adapter**: `src/DXOS.Infrastructure/Integrations/FacebookLeadAdsClient.cs` handles HMAC-SHA256 signature verification, Graph API `GET /{leadgen_id}` requests, and `GET /{sender_id}` profile name fetching using standard `HttpClient` and `System.Text.Json`.
- **Application Ingest**: Extends `LeadService.IntakePlatformWebhookAsync` with idempotent deduplication via `webhook_events`.
- **Social CRM Persistence**: Ingests conversations and messages into `aiecos_social` schema (`pages`, `customers`, `conversations`, `messages`) and serves REST APIs for the AIECOS Social CRM Inbox (`/admin` and `/inbox`).

```
Meta Webhook / Testing Tool / Live Messenger
       │ (POST /integrations/facebook/webhook)
       ▼
MarketingEndpoints.cs ──(Verify X-Hub-Signature-256)──► FacebookLeadAdsClient
       │
       ├─► (Fetch GET /v21.0/{leadgen_id} or Profile if Page Token present)
       │
       ├─► Ingest into aiecos_social (pages, customers, conversations, messages)
       │     └─► Realtime display on http://localhost:8080/inbox
       │
       ▼
LeadService.IntakePlatformWebhookAsync
       │
       ├─► Deduplicate leadgen_id / message_id via WebhookEventStore
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
FACEBOOK_PAGE_ID=988656934325292
FACEBOOK_PAGE_ACCESS_TOKEN=<your-page-access-token>
FACEBOOK_VERIFY_TOKEN=dxos_marketing_verify_token_2026

# Mode: 'mock' (default fallback) or 'live' (calls Graph API)
FACEBOOK_MODE=live

# Public HTTPS URL for Webhook Callback (via ngrok or cloudflared)
PUBLIC_BASE_URL=https://subepiglottal-rebeca-morally.ngrok-free.dev
```

---

## 3. Webhook Endpoints & Social CRM REST APIs

### 1) Verification Challenge (GET)
- **URL**: `{PUBLIC_BASE_URL}/integrations/facebook/webhook`
- **Parameters**: `hub.mode=subscribe`, `hub.verify_token=<token>`, `hub.challenge=<challenge>`
- **Response**: HTTP 200 with raw challenge text when tokens match; HTTP 401 when tokens mismatch.

### 2) Webhook Event Ingest (POST)
- **URL**: `{PUBLIC_BASE_URL}/integrations/facebook/webhook`
- **Security**: Verifies `X-Hub-Signature-256` header (HMAC SHA-256 computed with `FACEBOOK_APP_SECRET`).
- **Handles**:
  - `entry[].changes[].field == "leadgen"`: Extracts form lead data, scores lead in DX-OS, and records in `aiecos_social`.
  - `entry[].messaging[]`: Extracts Messenger chat messages, syncs customer & conversation, and records message in `aiecos_social`.

### 3) Social CRM REST APIs (AIECOS Admin UI Compatible)
- `GET /pages`: Connected Fanpages (e.g. Royce Shop).
- `GET /customers`: Customer profiles synced from chat and form leads.
- `GET /conversations`: Conversations with latest snippet and message count.
- `GET /messages`: Message history.
- `GET /inbox`: Direct access to AIECOS Social CRM Web UI on `http://localhost:8080/inbox`.

---

## 4. How to Test

1. Start your local tunnel:
   ```powershell
   ngrok http 8080
   ```
2. In Meta App Dashboard (`https://developers.facebook.com/apps/2184057745525782/webhooks/`):
   - Set Callback URL to `{YOUR_NGROK_HTTPS_URL}/integrations/facebook/webhook`
   - Set Verify Token to `FACEBOOK_VERIFY_TOKEN`.
   - Subscribe to `leadgen` and `messages` fields.
3. Open **Lead Ads Testing Tool**:
   - URL: `https://developers.facebook.com/tools/lead-ads-testing`
   - Select Page `Royce Shop` and your Lead Form.
   - Click **Create Lead** / **Tạo khách hàng tiềm năng**.
4. Open the Operator Board at `http://localhost:8080/board.html` or the Social CRM Inbox at `http://localhost:8080/inbox/index.html` to view the ingested leads and conversations in real time!

