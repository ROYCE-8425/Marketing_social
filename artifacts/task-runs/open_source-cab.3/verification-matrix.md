# BR001-R4 Verification Execution Matrix

- **Run ID**: `33c7c91ea2e3`
- **Start Time (UTC)**: `2026-08-15T06:33:42.0429733+00:00`
- **End Time (UTC)**: `2026-08-15T06:38:37.5979473+00:00`
- **Total Duration**: `295.55s`
- **Overall Status**: **PASSED (29/29 Steps Successful)**

| Step | Name | Command | Exit Code | Expected | Duration (s) | Status |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | dotnet-version | `dotnet --version` | 0 | 0 | 0.12 | PASS |
| 2 | nuget-sources | `dotnet nuget list source` | 0 | 0 | 0.24 | PASS |
| 3 | restore-locked-mode | `dotnet restore DXOS.slnx --locked-mode` | 0 | 0 | 0.92 | PASS |
| 4 | format-whitespace-verify | `dotnet format whitespace DXOS.slnx --verify-no-changes --no-restore` | 0 | 0 | 3.64 | PASS |
| 5 | build-release | `dotnet build DXOS.slnx -c Release --no-restore -warnaserror` | 0 | 0 | 1.26 | PASS |
| 6 | list-packages-transitive | `dotnet list DXOS.slnx package --include-transitive` | 0 | 0 | 2.56 | PASS |
| 7 | project-references | `Project XML AST reference scanner` | 0 | 0 | 0.06 | PASS |
| 8 | boundary-and-version-scan | `Full repository path and package version auditor` | 0 | 0 | 2.98 | PASS |
| 9 | ef-migrations-list | `dotnet tool run dotnet-ef migrations list (ephemeral DB)` | 0 | 0 | 3.92 | PASS |
| 10 | ef-database-update | `dotnet tool run dotnet-ef database update (ephemeral DB)` | 0 | 0 | 4.03 | PASS |
| 11 | postgres-live-probe | `GET /health/live (process liveness probe verification)` | 0 | 0 | 8.51 | PASS |
| 12 | postgres-ready-probe | `GET /health/ready (active PostgreSQL query probe verification)` | 0 | 0 | 7.93 | PASS |
| 13 | workflow-smoke-compose | `POST /smoke/workflow (Elsa 3.7.1 workflow execution verification)` | 0 | 0 | 8.05 | PASS |
| 14 | postgres-negative-health | `Negative DB Dependency Test (503 on ready/smoke, 200 on live)` | 0 | 0 | 23.97 | PASS |
| 15 | docker-compose-config-missing-secret | `docker compose -f compose.yaml config (without POSTGRES_PASSWORD)` | 1 | 1 | 0.11 | PASS |
| 16 | docker-compose-config-valid-secret | `docker compose -f compose.yaml config (with generated secret)` | 0 | 0 | 0.11 | PASS |
| 17 | smoke-runtime-compose | `powershell.exe -File scripts/smoke-runtime.ps1 -Mode Compose` | 0 | 0 | 7.9 | PASS |
| 18 | smoke-runtime-aspire | `powershell.exe -File scripts/smoke-runtime.ps1 -Mode Aspire` | 0 | 0 | 17.41 | PASS |
| 19 | image-inspect-digests | `docker image inspect PostgreSQL, SDK 10, ASP.NET 10 digests` | 0 | 0 | 0.24 | PASS |
| 20 | docker-resource-audit | `Exact pre/post Docker container, network, volume canonical state comparison` | 0 | 0 | 1.89 | PASS |
| 21 | verify-check-contract | `powershell.exe -File scripts/verify-check-contract.ps1` | 0 | 0 | 88.36 | PASS |
| 22 | check-profile-foundation | `powershell.exe -File scripts/check.ps1 -Profile Foundation` | 0 | 0 | 8.44 | PASS |
| 23 | check-profile-runtime | `powershell.exe -File scripts/check.ps1 -Profile Runtime` | 1 | 1 | 34.64 | PASS |
| 24 | check-profile-full | `powershell.exe -File scripts/check.ps1 -Profile Full` | 1 | 1 | 31.64 | PASS |
| 25 | openspec-validate | `openspec.cmd validate bootstrap-remediation-001 --type change --strict --no-interactive` | 0 | 0 | 1.24 | PASS |
| 26 | beads-show | `bd.cmd show open_source-cab.3 --json` | 0 | 0 | 0.93 | PASS |
| 27 | beads-dep-cycles | `bd.cmd dep cycles` | 0 | 0 | 0.34 | PASS |
| 28 | git-diff-check-and-status | `git diff --check && git status --short` | 0 | 0 | 0.09 | PASS |
| 29 | secret-and-encoding-scan | `Strict UTF-8, mojibake, control character, and secret pattern scan` | 0 | 0 | 1.32 | PASS |
