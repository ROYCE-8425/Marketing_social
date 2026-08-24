# Facebook Page Operator Agent Architecture (DX-OS)

## 1. Product Context & Philosophy

**DX-OS** is an open-source Marketing Operating System tailored for SMEs, starting with Facebook-first operations for **Royce Shop** (`988656934325292`). 

The AI Agent in DX-OS is designed as an **Operator Assistant / Chuyên gia đồng hành**, not an autonomous black-box swarm. 

### Core Product Invariants
1. **Fixed Operator Loop**:
   $$\text{Observe} \longrightarrow \text{Reason} \longrightarrow \text{Propose Actions} \longrightarrow \text{Human Confirms} \longrightarrow \text{Endpoint Executes}$$
2. **Strict Human Gate**: The AI agent **MUST NOT** publish posts, schedule posts, send Messenger messages, allocate ad budgets, delete content, or mutate user roles autonomously. Every state-changing action requires an explicit human click by an authenticated user with verified RBAC permissions.
3. **No Direct SDK Coupling in Domain**: The Domain and Application layers have zero dependencies on vendor LLM SDKs or Facebook SDKs. AI capabilities are abstracted behind `IChatClient` (currently backed by Google Gemini `gemini-2.5-flash-lite` via `HttpClient`, with `MockChatClient` for testing).
4. **Honest Data & Permissions**: The agent never fabricates interaction counts, follower numbers, or private user comments. If Meta Graph API scopes (`pages_read_user_content`) are missing or restricted, the agent acknowledges the limitation (`commentsStatus: "forbidden"|"unknown"`) rather than hallucinating replies.

---

## 2. Tool Catalog (Read & Propose Only)

In DX-OS, agent tools are modeled as typed Application methods wrapping existing read-only services. The model can request tool executions during its reasoning rounds:

| Tool Name | Method Port | Description | Scope / Permission |
| :--- | :--- | :--- | :--- |
| `page_health` | `PageHealthService.GetHealthEvaluationWithStatusAsync` | Retrieves 4-axis health scores (Content, Engagement, Inbox, Leads), status label, and reasons. | `page.posts.read` |
| `inbox_unreplied` | `PageHealthService.GetInboxActionsAsync` | Fetches up to 5 unreplied customer conversations, detected phone numbers, and draft reply templates. | `inbox.read` |
| `list_posts` | `IPageHealthStore.GetHealthDataAsync` | Reads recent 5 social posts with truncated messages (≤ 200 chars), reaction, comment, and share counts. | `page.posts.read` |
| `draft_inbox` | `PageHealthService` draft lookup | Proposes targeted reply for a specific conversation ID. Does not send or mutate data. | `inbox.reply` |

### Strictly Forbidden Capabilities (Hard Banned)
* `publish_post` / `schedule_post`
* `send_message` / `send_reply`
* `delete_*` / `modify_*`
* `spend_*` / `create_ad`
* `change_roles` / `assign_permissions`
* Any write call to Meta Graph API or internal mutations without human confirmation.

---

## 3. Output Contract

Every invocation of the Page Agent produces a deterministic, typed JSON contract:

```json
{
  "summary": "1-3 Vietnamese summary sentences highlighting key observation.",
  "focus": "inbox | content | leads | engagement | data",
  "actions": [
    {
      "id": "a1",
      "type": "reply_inbox | compose_post | sync_page | ask_owner | wait",
      "title": "Short descriptive title of the proposed action",
      "rationale": "Why this action is recommended based on health data",
      "payload": {
        "conversationId": "conv_123",
        "suggestedReply": "Draft message content for inbox",
        "suggestedPost": "Draft post caption for Facebook"
      },
      "requiresPermission": "inbox.reply | page.publish | page.posts.read",
      "autoExecute": false
    }
  ],
  "disclaimer": "AI không tự đăng bài, không tự gửi tin, không chi tiền."
}
```

### Invariants:
* `autoExecute`: **Always `false`**.
* `actions`: Maximum **5 actions**.
* `action.type`: Strictly restricted to `{"reply_inbox", "compose_post", "sync_page", "ask_owner", "wait"}`. Any unauthorized types are filtered out.
* `disclaimer`: Fixed safety disclosure rendered on every client interface.
* Failsafe: If the model output cannot be parsed as JSON after 3 rounds, the service gracefully returns a fallback payload with 1 `"wait"` action and the raw text in `summary`.

---

## 4. Wave B Implementation (Bounded Tool Loop)

Wave B replaces the static single-shot evidence dump with a dynamic, bounded multi-turn tool loop (maximum 3 rounds):

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Client / UI (http://127.0.0.1:8080/admin/)               │
│    User clicks "Chạy agent" (Requires: page.posts.read)     │
└──────────────────────────────┬──────────────────────────────┘
                               │ POST /facebook/page/agent/run
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. DXOS.Api (MarketingEndpoints.cs)                         │
│    RBAC Check (AppPermissions.PagePostsRead)                │
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. DXOS.Application (PageAgentService.cs)                   │
│    Bounded Tool Loop (Rounds 1..3):                         │
│    ┌───────────────────────────────────────────────────┐    │
│    │ for round = 1 to 3:                               │    │
│    │   1. Call IChatClient.CompleteAsync               │    │
│    │   2. If tool JSON: {"tool": "<name>", "args": {}} │    │
│    │      -> Execute Application tool method           │    │
│    │      -> Append tool result to conversation context│    │
│    │      -> Record tool name in toolTrace             │    │
│    │   3. If final JSON:                               │    │
│    │      -> ParseAgentResponse(clean)                 │    │
│    │      -> Break loop                                │    │
│    └───────────────────────────────────────────────────┘    │
│    - Enforce autoExecute=false & fixed Disclaimer           │
│    - Return PageAgentRunResponse(agent, eval, status, trace)│
└──────────────────────────────┬──────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. Client UI Action Cards & Tool Trace                      │
│    - "Đã dùng N tool: page_health, inbox_unreplied"         │
│    - reply_inbox: "Chép tin", "Gửi tin này", "Mở hộp thư"   │
│    - compose_post: "Chép vào ô đăng bài"                    │
│    - Handed off to existing human-gated endpoints           │
└─────────────────────────────────────────────────────────────┘
```

---

## 5. Wave C Roadmap (Future Authentication & Governance)

* **Login / OIDC & Multi-tenant Identity**: Integrate standard OAuth2 / OIDC providers for multi-user organizational login with session token issuance (out of scope for Wave B).
* **Audit Trail Persistence**: Log each agent run session along with its `toolTrace` and confirmed human decisions into a durable audit table.
