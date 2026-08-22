# Third-Party Services

Status: RECONCILED

External third-party services and developer tools are disclosed separately from open-source dependencies. Use of proprietary development tooling does not alter the Apache-2.0 license decision for DX-OS source code.

## Disclosed Services and Tools

| Service / Tool | Category | Purpose | Runtime Required | Data Boundary | Cost / Access Assumption | Fallback / Replacement |
|---|---|---|---|---|---|---|
| Google Gemini | Development-time AI tooling | Bootstrap implementation assistance | No | Repository content supplied during authorized developer sessions | Provider account/access may be required for development only | Human implementation or another approved coding assistant |
| OpenAI / Codex | Development-time AI tooling | Architecture review, verification, and dual-agent validation | No | Repository content supplied during authorized developer sessions | Provider account/access may be required for development only | Human review or another approved coding assistant |

## Runtime Service Boundaries

- **Mandatory Paid Services**: Zero proprietary paid runtime services (email, SMS, cloud hosting, SaaS APIs) are required to build, test, run, or demo DX-OS.
- **AI Integrations**: All AI capabilities integrate through provider-independent abstractions (`IChatClient`), adhering strictly to [ADR-0002](adr/0002-third-party-services-and-ai-provider-independence.md).
- **Transparency**: Any future external service dependency must be reviewed and disclosed in this document within the same PR that introduces it.
