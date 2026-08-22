# ADR-0007: Dual Orchestration via Aspire and Docker Compose

- Status: Accepted
- Date: 2026-08-14
- Decision owners: DX-OS project owner and maintainers

## Context

Developers need a fast inner-loop experience for debugging and tracing, while demo, CI, and production deployments require deterministic, reproducible container orchestration.

## Decision

DX-OS supports dual orchestration paths targeting identical service contracts:

1. **Developer Inner-Loop (Aspire)**:
   - src/DXOS.AppHost orchestrates API and PostgreSQL container resources using .NET Aspire 13.4.
   - Provides OpenTelemetry dashboards, log streaming, and local debugging.
2. **Deployment and Demo Orchestration (Docker Compose)**:
   - compose.yaml orchestrates PostgreSQL and containerized DXOS.Api services.
   - Uses pinned container images and digest-verified base images.
   - Mandatory for clean-clone verification and release readiness gates.
3. **Resource Lifecycle & Fail-Closed Cleanup**:
   - Automated scripts (scripts/smoke-runtime.ps1) enforce bounded startup timeouts and deterministic cleanup of task-owned containers.

## Consequences

- Developers have native IDE debugging with Aspire while release verification runs standard Docker Compose.
- Both paths configure identical environment variables and PostgreSQL database connections.
- Docker environment is a required prerequisite for full runtime and integration verification.

## Verification

- Validate Docker Compose configuration with docker compose -f compose.yaml config.
- Verify Aspire AppHost builds and runs with dotnet build src/DXOS.AppHost/DXOS.AppHost.csproj.
- Execute smoke verification across both Aspire and Compose modes.