## Purpose

Defines the reproducible workflow, database, local orchestration, container, configuration, and health behavior required for the DX-OS Engineering OS runtime spike.

## ADDED Requirements

### Requirement: Elsa is consumed only through stable NuGet packages
DX-OS SHALL consume the minimum approved stable Elsa packages through Central Package Management and MUST NOT reference Elsa source projects or floating/preview versions without a separately accepted ADR.

#### Scenario: Elsa dependency graph is inspected
- **WHEN** package and project references are enumerated
- **THEN** Elsa appears only as approved NuGet package references at workflow/composition boundaries
- **AND** no path points to an Elsa source project

### Requirement: Elsa smoke workflow is executable
The runtime SHALL expose a deterministic smoke workflow that can be invoked during local and CI verification and produces an observable successful completion result.

#### Scenario: Smoke workflow succeeds
- **WHEN** the documented workflow smoke command runs against the supported bootstrap runtime
- **THEN** a workflow instance starts and completes
- **AND** the verification records its identifier, terminal status, and trace or log correlation evidence

#### Scenario: Workflow dependency is unavailable
- **WHEN** required workflow persistence or runtime configuration is unavailable
- **THEN** the smoke check fails non-zero with the failed dependency identified

### Requirement: PostgreSQL is the authoritative bootstrap database
The runtime SHALL use PostgreSQL through approved .NET data-access packages, apply reviewed migrations, and expose a readiness check that includes a real database operation.

#### Scenario: PostgreSQL is ready
- **WHEN** the database is reachable with valid configuration
- **THEN** readiness succeeds only after a query or equivalent database operation completes

#### Scenario: PostgreSQL is unreachable
- **WHEN** the database endpoint is unavailable or credentials are invalid
- **THEN** readiness reports unhealthy and dependent smoke checks fail non-zero

### Requirement: Database configuration is secret-safe
Database credentials and other secrets MUST be supplied through documented local secret or environment mechanisms and MUST NOT be committed to source, Compose defaults, logs, test evidence, or task prompts.

#### Scenario: Repository is scanned for secrets
- **WHEN** the deterministic secret scan runs on the clean clone and Git history
- **THEN** no real credential or token is reported
- **AND** example values are clearly synthetic or variable references

### Requirement: Aspire is a real developer control plane
The AppHost SHALL orchestrate the approved DX-OS resources, surface health state, and allow an agent or developer to obtain logs/traces for the API, PostgreSQL, and workflow smoke path.

#### Scenario: Aspire starts successfully
- **WHEN** the documented Aspire command runs in a supported developer environment
- **THEN** required resources reach their declared healthy state
- **AND** runtime evidence is discoverable through documented endpoints or commands

### Requirement: Docker Compose is a reproducible demo path
The repository SHALL provide a valid `compose.yaml` that starts the required bootstrap services without relying on the old Elsa checkout or undocumented paid infrastructure.

#### Scenario: Compose configuration is validated
- **WHEN** `docker compose -f compose.yaml config` runs
- **THEN** validation succeeds without missing files, unresolved required variables, or references outside the repository

#### Scenario: Compose runtime starts
- **WHEN** the documented Compose startup runs with supported prerequisites
- **THEN** PostgreSQL and the required DX-OS services become healthy
- **AND** the workflow smoke check can execute against that environment

### Requirement: Aspire and Compose have explicit roles
Aspire SHALL be the developer orchestration and observability path, while Docker Compose SHALL be the reproducible local/demo deployment path; neither path may require the other to be installed at runtime.

#### Scenario: One orchestration path is unavailable
- **WHEN** a supported developer uses only the other documented orchestration path
- **THEN** its own startup and smoke verification remain executable

### Requirement: Runtime startup is observable and bounded
Required services SHALL emit structured startup, health, database, and workflow evidence without logging secrets or unnecessary personal data, and SHALL fail within documented timeouts when dependencies cannot become ready.

#### Scenario: Dependency startup times out
- **WHEN** a required resource does not become ready within the documented limit
- **THEN** startup verification fails non-zero
- **AND** the evidence identifies the resource and correlation data needed to diagnose it

### Requirement: Third-party services are explicit and separable
External services and proprietary APIs MUST be documented separately from OSS dependencies, including development AI tools, email/SMS providers, advertising APIs, cloud services, SaaS, and every proprietary API used by the demo. An undisclosed paid or proprietary service MUST NOT be a mandatory runtime dependency.

#### Scenario: External service is introduced
- **WHEN** a development, demo, or runtime path uses an external service
- **THEN** the service, provider, purpose, data boundary, requirement level, cost/access assumption, and fallback are disclosed
- **AND** the service is not misclassified as an open-source dependency

### Requirement: AI integrations are provider-independent
Domain and business modules MUST depend on a DX-OS-owned AI abstraction rather than a Gemini-, OpenAI-, or vendor-specific SDK. Where practical, adapters SHALL support Gemini, an OpenAI-compatible provider, a local/open-weight model, and future providers without rewriting business modules.

#### Scenario: AI provider changes
- **WHEN** an approved AI provider adapter is replaced or disabled
- **THEN** domain behavior and business-module contracts remain unchanged
- **AND** provider-specific configuration and SDK types remain outside the domain boundary
