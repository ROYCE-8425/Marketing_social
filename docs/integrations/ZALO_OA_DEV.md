# Zalo Official Account (Official OpenAPI) Integration Guide

**Status:** IMPLEMENTED & VERIFIED  
**Zalo App Name:** `DX-OS Marketing Dev`  
**Target OA:** `Royce Shop`  
**API Version:** `v3.0`  
**Documentation:** https://developers.zalo.me/docs/api/official-account-api-230  

---

## 1. Architecture Overview

DX-OS connects directly to Zalo's official OpenAPI and Webhook infrastructure:
- **Zero Domain Pollution**: Domain layer (`src/DXOS.Domain`) has zero knowledge of Zalo or OpenAPI payload types.
- **Infrastructure Adapter**: `src/DXOS.Infrastructure/Integrations/ZaloOaClient.cs` handles HMAC-SHA256 signature verification, OpenAPI `POST /v3.0/oa/message/cs` message sending, and `GET /v3.0/oa/user/detail` profile fetching using standard `HttpClient` and `System.Text.Json`.
- **Application Ingest**: Extends `LeadService.IntakePlatformWebhookAsync("zalo", ...)` with idempotent deduplication via `webhook_events`.
- **Social CRM Persistence**: Ingests conversations and messages into `SocialPages`, `SocialCustomers`, `SocialConversations`, and `SocialMessages` tables, served directly to the DX-OS Social CRM Inbox (`/inbox`).

```
Zalo Webhook (user_send_text, user_send_image, follow, unfollow)
       │ (POST /integrations/zalo/webhook)
       ▼
MarketingEndpoints.cs ──(Verify X-ZEP-Signature / mac)──► ZaloOaClient
       │
       ├─► (Fetch GET /v3.0/oa/user/detail if OA Access Token present)
       │
       ├─► Ingest into Social CRM (pages, customers, conversations, messages)
       │     └─► Realtime display on http://localhost:8080/inbox
       │
       ▼
LeadService.IntakePlatformWebhookAsync
       │
       ├─► Deduplicate message_id via WebhookEventStore
       ├─► Score Lead (HOT/WARM/COLD) via LeadScoring
       └─► Persist Lead via LeadStore
```

---

## 2. Configuration & Environment Variables

Configure in local `.env` (gitignored, never committed):

```env
# ── Zalo Official Account (Official OpenAPI) ──
ZALO_APP_ID=
ZALO_APP_SECRET=
ZALO_OA_ID=
ZALO_OA_ACCESS_TOKEN=
ZALO_OA_SECRET=
ZALO_MODE=live
```

---

## 3. Webhook Endpoints & Social CRM REST APIs

### 1) Verification Challenge / Health Check (GET)
- **URL**: `{PUBLIC_BASE_URL}/integrations/zalo/webhook`
- **Response**: Returns challenge parameter if present or JSON status `{ "status": "OK", "provider": "zalo" }`.

### 2) Webhook Event Ingest (POST)
- **URL**: `{PUBLIC_BASE_URL}/integrations/zalo/webhook`
- **Security**: Verifies `X-ZEP-Signature` / `X-Zalo-Signature` / `mac` header (HMAC SHA-256 computed with `ZALO_OA_SECRET` or `ZALO_APP_SECRET`).
- **Handles**:
  - `user_send_text`: Extracts user message, creates conversation `zalo_{oaId}_{userId}`, syncs customer & message.
  - `user_send_image` / media: Extracts attachment image URL, syncs message.
  - `follow` / `unfollow`: Updates customer status and creates notice in conversation thread.

### 3) Outbound Message Sending (POST /conversations/{id}/messages)
- For conversations with prefix `zalo_`:
  - Calls Zalo Customer Support OpenAPI: `POST https://openapi.zalo.me/v3.0/oa/message/cs`
  - Sends text to recipient `user_id` using `ZALO_OA_ACCESS_TOKEN`.
  - Ingests agent response message into database and updates conversation snippet.

---

## 4. How to Test End-to-End

1. Start your local tunnel:
   ```powershell
   ngrok http 8080
   ```
2. In Zalo Developer Portal (`https://developers.zalo.me/`):
   - Set Webhook URL to `{PUBLIC_HTTPS}/integrations/zalo/webhook`.
   - Subscribe to `user_send_text`, `user_send_image`, `follow`, `unfollow`.
3. Open Zalo mobile app:
   - Search and follow OA **Royce Shop**.
   - Send: `"Xin chao OA"`.
4. Open the Social CRM Inbox at `http://localhost:8080/inbox/`:
   - The message from the Zalo customer appears in real time.
   - Reply `"Shop da nhan tin"` from the inbox.
   - The reply is sent back to the user's mobile Zalo app via Zalo OpenAPI.
