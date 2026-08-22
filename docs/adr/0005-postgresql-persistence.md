# ADR-0005: PostgreSQL Persistence and Database Architecture

- Status: Accepted
- Date: 2026-08-14
- Decision owners: DX-OS project owner and maintainers

## Context

DX-OS requires reliable relational persistence for system state, entity tracking, and workflow runtime coordination. Using in-memory databases for tests or development creates behavioral drift compared to production environments.

## Decision

PostgreSQL 18.4 is the standard relational database for DX-OS:

1. **Data Access**: Entity Framework Core 10 (Microsoft.EntityFrameworkCore) with Npgsql provider (Npgsql.EntityFrameworkCore.PostgreSQL).
2. **Migrations**: Schema evolution is managed via explicit EF Core code-first migrations located in src/DXOS.Infrastructure/Migrations.
3. **Health Probes**: Liveness (/health/live) checks process responsiveness; Readiness (/health/ready) executes an active database roundtrip query (SELECT 1).
4. **Integration Testing**: Real PostgreSQL container instances are provisioned on-demand during testing using Testcontainers.PostgreSql. In-memory database mocks are prohibited for persistence tests.

## Consequences

- Eliminates test/production behavioral divergence.
- Requires Docker runtime for integration testing and containerized execution.
- Integration tests execute against real PostgreSQL instances with automated container lifecycle and cleanup.

## Verification

- Execute database migrations via BootstrapDbContextFactory.
- Verify health check endpoints under healthy and disconnected database conditions.
- Run dotnet test tests/DXOS.Integration.Tests/DXOS.Integration.Tests.csproj against Testcontainers PostgreSQL.