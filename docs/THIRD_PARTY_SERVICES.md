# Third-Party Services

Status: RECONCILED

External third-party services and developer tools are disclosed separately from open-source dependencies. Use of proprietary development tooling does not alter the Apache-2.0 license decision for DX-OS source code.

## Disclosed Services and Tools

| Service / Tool | Category | Purpose | Runtime Required | Data Boundary | Cost / Access Assumption | Fallback / Replacement |
|---|---|---|---|---|---|---|
| Google Gemini | Development-time AI tooling | Bootstrap implementation assistance | No | Repository content supplied during authorized developer sessions | Provider account/access may be required for development only | Human implementation or another approved coding assistant |
| OpenAI / Codex | Development-time AI tooling | Architecture review, verification, and dual-agent validation | No | Repository content supplied during authorized developer sessions | Provider account/access may be required for development only | Human review or another approved coding assistant |
| Meta Graph API / Lead Ads | External Webhook & Marketing API | Optional official lead ads ingest (Dev mode Page) | No (Optional) | Form submission fields (name, phone, email) from user-authorized Facebook Page | Meta Developer Account (Free / Dev mode) | Built-in mock webhook simulator (`FACEBOOK_MODE=mock`) |
| Zalo Official Account OpenAPI | External Webhook & Messaging API | Optional official Zalo OA message intake and customer support reply | No (Optional) | User chat messages (text, media) and follower events from authorized Zalo OA | Zalo Developer Account (Free / Dev mode) | Built-in mock webhook simulator (`ZALO_MODE=mock`) |
| TikTok Marketing API / Lead Gen | External Webhook & Marketing API | Optional official TikTok lead generation intake | No (Optional) | Form submission fields (name, phone, email) from authorized TikTok Advertiser | TikTok for Business Account (Free / Dev mode) | Built-in mock webhook simulator (`TIKTOK_MODE=mock`) |
| Moonshot / Kimi Chat API | Optional runtime LLM | Page advisor + inbox draft text via `IChatClient` (no publish/send) | No (Optional) | Fanpage evaluation reasons and conversation snippets sent to Moonshot when `KIMI_API_KEY` is set and Gemini is not configured | Moonshot/Kimi developer key (paid usage) | Gemini (`GEMINI_API_KEY`) or `MockChatClient` |
| Google Gemini API | Optional runtime LLM | Page advisor + inbox draft text via `IChatClient` (no publish/send). Default free-tier model `gemini-2.5-flash-lite` | No (Optional) | Fanpage evaluation reasons and conversation snippets sent to Google when `GEMINI_API_KEY` is set | Google AI Studio key (free-tier rate limits) | `MockChatClient` when no Gemini/Kimi key |

## Runtime Service Boundaries

- **Mandatory Paid Services**: Zero proprietary paid runtime services (email, SMS, cloud hosting, SaaS APIs) are required to build, test, run, or demo DX-OS. Kimi/Moonshot is optional; the clone path uses `MockChatClient`.
- **AI Integrations**: All AI capabilities integrate through provider-independent abstractions (`IChatClient`), adhering strictly to [ADR-0002](adr/0002-third-party-services-and-ai-provider-independence.md).
- **Transparency**: Any future external service dependency must be reviewed and disclosed in this document within the same PR that introduces it.
