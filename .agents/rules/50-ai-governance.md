# AI Governance Rules

- All AI capabilities must be accessed via DX-OS-owned abstractions (e.g. IChatClient / application ports).
- Application and domain logic must remain independent of specific AI providers (Gemini, OpenAI, Anthropic, local models).
- Third-party AI services must be disclosed in docs/THIRD_PARTY_SERVICES.md per ADR-0002.
- AI operations may classify, summarize, extract, or recommend data.
- AI operations must NOT permanently delete data, execute financial transactions, modify ad budgets, or publish external content without explicit user approval.