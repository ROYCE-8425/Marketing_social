# ADR-0002: Third-Party Services and AI Provider Independence

- Status: Accepted
- Date: 2026-08-13

## Context

DX-OS may use proprietary development tools and may integrate external services. These are not OSS dependencies and must not be hidden inside an open-source claim. Business logic also must not become permanently coupled to one model provider.

## Decision

DX-OS maintains a separate third-party-service disclosure covering development AI tools, email/SMS providers, advertising APIs, cloud services, SaaS, and proprietary APIs used by the demo or runtime. Each entry records provider, purpose, data boundary, whether it is required at runtime, access/cost assumptions, and fallback or replacement strategy.

AI capabilities use a DX-OS-owned port at the application boundary. Provider SDK types and credentials remain in adapters/infrastructure. Where practical, the adapter contract permits Gemini, an OpenAI-compatible provider, a local/open-weight model, and future providers without changing domain or business-module contracts.

## Consequences

- Gemini and OpenAI/Codex may be disclosed as development tooling without changing the DX-OS source license.
- No proprietary paid service may become an undocumented mandatory runtime dependency.
- Provider-specific behavior belongs in adapters; the abstraction must express DX-OS needs rather than the union of vendor SDKs.
- Provider selection, credentials, data handling, and fallback are configuration and operations concerns.

## Verification

- Review domain/application assemblies for provider SDK references.
- Compare runtime configuration and demo instructions with the service disclosure.
- Exercise at least one replaceability test or fake/local adapter before a provider-backed feature is accepted.
- Fail release when a required service is undisclosed or private-source access is necessary.
