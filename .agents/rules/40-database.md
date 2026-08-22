# Database & Persistence Rules

- Database: PostgreSQL 18.4 via Npgsql and EF Core 10.
- Schema changes must be managed through explicit EF Core migrations.
- Database access and DbContext factories must reside in src/DXOS.Infrastructure.
- Health probes must execute real database queries for readiness validation.
- Connection strings must be configurable via environment variables without hardcoded secrets.