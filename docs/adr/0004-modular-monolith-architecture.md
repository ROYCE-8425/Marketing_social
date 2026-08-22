# ADR-0004: Modular Monolith Architecture

- Status: Accepted
- Date: 2026-08-13
- Decision owners: DX-OS project owner and maintainers

## Context

Enterprise workflow systems often suffer from premature microservice decomposition or excessive Clean Architecture boilerplate (e.g., generic repository wrappers, UnitOfWork abstractions over EF Core, empty IService/Service interfaces). DX-OS requires clear module boundaries with proportionate architecture that avoids unnecessary ceremony.

## Decision

DX-OS adopts a modular monolith architecture structured around vertical feature slices and explicit architectural layers:

1. **Layer Hierarchy**:
   - DXOS.Domain: Pure domain entities, value objects, and business invariants with zero external dependencies.
   - DXOS.Application: Application use cases, commands, queries, and port interfaces.
   - DXOS.Infrastructure: Database persistence (BootstrapDbContext), EF Core migrations, and external service adapters.
   - DXOS.Workflows: Workflow definitions and custom activities utilizing Elsa NuGet packages.
   - DXOS.Api: ASP.NET Core web endpoints, middleware, and health probe composition.
   - DXOS.AppHost: .NET Aspire developer orchestration.
2. **Proportionate Boundaries**:
   - Prohibit generic repositories and UnitOfWork wrappers over EF Core DbContext.
   - Prohibit empty one-to-one service interfaces lacking polymorphism or test necessity.
   - Prohibit premature microservice decomposition.
3. **Automated Enforcement**:
   - Architectural constraints are enforced via ArchUnitNET rules in tests/DXOS.Architecture.Tests.

## Consequences

- Clear boundary enforcement protects domain logic from framework and infrastructure churn.
- Codebase remains lightweight and straightforward to navigate for both agents and human developers.
- Compilation and test execution times remain fast.

## Verification

- Run dotnet test tests/DXOS.Architecture.Tests/DXOS.Architecture.Tests.csproj to enforce directional dependencies and anti-pattern bans.