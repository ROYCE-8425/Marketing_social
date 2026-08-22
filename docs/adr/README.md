# DX-OS Architecture Decision Records

ADRs record durable DX-OS decisions. The DX-OS Constitution (.specify/memory/constitution.md) is higher authority; an ADR cannot weaken a constitutional MUST requirement.

| ADR | Status | Decision |
|---|---|---|
| [ADR-0001](0001-dx-os-open-source-license.md) | Accepted | DX-OS is open source under Apache-2.0 by default |
| [ADR-0002](0002-third-party-services-and-ai-provider-independence.md) | Accepted | Services are disclosed separately and AI boundaries are provider-independent |
| [ADR-0003](0003-independent-repository-extraction-and-identity.md) | Accepted | Independent DX-OS repository extraction, clean history, and distinct identity |
| [ADR-0004](0004-modular-monolith-architecture.md) | Accepted | Modular monolith with vertical slices and proportionate boundaries |
| [ADR-0005](0005-postgresql-persistence.md) | Accepted | PostgreSQL persistence via EF Core and Testcontainers integration |
| [ADR-0006](0006-elsa-nuget-integration.md) | Accepted | Elsa workflow engine consumption strictly via stable NuGet packages |
| [ADR-0007](0007-aspire-and-docker-compose-orchestration.md) | Accepted | Dual orchestration via .NET Aspire (inner loop) and Docker Compose (deployment) |
| [ADR-0008](0008-aiecos-foundation-dual-stack.md) | Accepted | AIECOS Node foundation is kept; DX-OS .NET 10 is the product OS |