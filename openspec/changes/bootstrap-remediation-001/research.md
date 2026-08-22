# Bootstrap Remediation Research

Research date: 2026-08-11. Scope is limited to version-sensitive decisions required by `bootstrap-remediation-001`. Sources are official product documentation, official package registries, and upstream repositories/advisories. No tool or package was installed as part of this research.

## Decision 1: .NET 10 SDK pin

- **Decision:** Pin SDK `10.0.302` with `rollForward: latestPatch` and `allowPrerelease: false`; target `net10.0` only.
- **Alternatives considered:** Use installed `10.0.103`; pin `10.0.110`; omit `global.json`; allow latest feature/minor band.
- **Reason:** The official .NET 10 page identifies 10.0.10 as the July 2026 security patch and SDK 10.0.302 as the current primary SDK. Pinning the 3xx feature band plus patch roll-forward is deterministic while permitting servicing patches. The 1xx band is also current but does not match the primary SDK/Visual Studio 18.8 toolchain selected for the new foundation.
- **Version:** SDK 10.0.302; runtime 10.0.10; C# 14.
- **License:** MIT for .NET SDK/runtime source, with bundled third-party notices applying.
- **Source:** [Official .NET 10 download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0), [global.json selection policy](https://learn.microsoft.com/en-us/dotnet/core/tools/global-json), [.NET SDK license](https://github.com/dotnet/sdk/blob/main/LICENSE.TXT).
- **Risk:** Existing machines with only 10.0.103 will fail the pin. The gate must report the required SDK; agents must not silently broaden roll-forward or auto-install it.

## Decision 2: Elsa integration and minimum smoke package

- **Decision:** Reference the stable `Elsa` bundle package `3.7.1` in `DXOS.Workflows` for the bootstrap smoke. Do not reference Elsa source projects or preview feeds.
- **Alternatives considered:** Directly reference `Elsa.Workflows.Core`, `Elsa.Workflows.Management`, and `Elsa.Workflows.Runtime`; use `3.8.0-preview1`; compile the existing source checkout.
- **Reason:** Elsa's official package guide calls `Elsa` the primary package and documents that it bundles the essential Core, Management, Runtime, Mediator, and API-common packages. The official Hello World uses only `dotnet add package Elsa` plus `AddElsa()` for a code-defined workflow. One stable direct dependency minimizes DX-OS package declarations while preserving a supported starting path. Component-level minimization is allowed later only if SBOM/size evidence justifies it.
- **Version:** 3.7.1 stable; preview 3.8.0-preview1 rejected.
- **License:** MIT.
- **Source:** [Elsa package guide](https://docs.elsaworkflows.io/getting-started/packages), [Elsa Hello World](https://docs.elsaworkflows.io/getting-started/hello-world), [Elsa 3.7.1 on NuGet](https://www.nuget.org/packages/Elsa/3.7.1).
- **Risk:** The bundle carries more transitive surface than Core alone. Record the resolved graph in the SBOM and do not expose Elsa APIs outside the workflow/composition boundary.

## Decision 3: Aspire AppHost and PostgreSQL orchestration

- **Decision:** Use `Aspire.Hosting.AppHost` and `Aspire.Hosting.PostgreSQL` `13.4.6` in a real `DXOS.AppHost` project.
- **Alternatives considered:** Keep the current console placeholder; use Compose as the only developer orchestrator; add all Aspire integrations preemptively.
- **Reason:** Official Aspire documentation models PostgreSQL with `AddPostgres(...).AddDatabase(...)` and passes connection data to a consuming project with `WithReference`, which matches the required developer control-plane role. Only AppHost and PostgreSQL hosting integrations are required now.
- **Version:** 13.4.6.
- **License:** MIT.
- **Source:** [Aspire integration overview](https://learn.microsoft.com/dotnet/aspire/fundamentals/integrations-overview), [add Aspire to an existing app](https://learn.microsoft.com/en-us/dotnet/aspire/get-started/add-aspire-existing-app), [AppHost package](https://www.nuget.org/packages/Aspire.Hosting.AppHost/13.4.6), [PostgreSQL hosting package](https://www.nuget.org/packages/Aspire.Hosting.PostgreSQL/13.4.6).
- **Risk:** Aspire APIs and package cadence are faster than .NET LTS. Pin exact stable versions and smoke AppHost startup in CI rather than treating package restore as proof.

## Decision 4: PostgreSQL server

- **Decision:** Use PostgreSQL `18.4` for Compose, Aspire resource configuration/evidence, and integration-test compatibility.
- **Alternatives considered:** PostgreSQL 17.10; floating `postgres:latest`; the inherited Elsa database matrix.
- **Reason:** PostgreSQL's official versioning page lists 18.4 as the current supported minor for major 18 and recommends always running the current minor. A specific version avoids mutable `latest` behavior; deployment should additionally pin a verified container digest when implemented.
- **Version:** 18.4.
- **License:** PostgreSQL License.
- **Source:** [PostgreSQL versioning policy](https://www.postgresql.org/support/versioning/), [PostgreSQL license](https://www.postgresql.org/about/licence/).
- **Risk:** Container tags can be mutable and architecture-specific. Record the resolved image digest and upgrade through reviewed tasks.

## Decision 5: EF Core and Npgsql

- **Decision:** Use EF Core `10.0.10` and `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.3`. Do not add a direct `Npgsql` reference unless production code actually uses its direct API.
- **Alternatives considered:** Inherited Npgsql provider 10.0.1; raw Npgsql only; EF InMemory; generic repository/UnitOfWork wrappers.
- **Reason:** EF Core 10.0.10 is the current stable .NET 10 servicing release. Npgsql provider 10.0.3 is the current stable net10 package and depends on the compatible EF Core 10 line. EF's `DbContext` already represents unit-of-work/repository semantics; wrappers would add ceremony without domain value.
- **Version:** Microsoft.EntityFrameworkCore 10.0.10; Npgsql.EntityFrameworkCore.PostgreSQL 10.0.3; transitive Npgsql 10.0.3.
- **License:** EF Core MIT; Npgsql/provider PostgreSQL License.
- **Source:** [EF Core package](https://www.nuget.org/packages/Microsoft.EntityFrameworkCore/10.0.10), [Npgsql EF provider](https://www.nuget.org/packages/Npgsql.EntityFrameworkCore.PostgreSQL/10.0.3), [Npgsql package](https://www.nuget.org/packages/Npgsql/10.0.3).
- **Risk:** Provider and EF patch cadences differ. Restore must prove the resolved graph, and the PostgreSQL integration test must catch provider/runtime incompatibility.

## Decision 6: PostgreSQL integration tests

- **Decision:** Use `Testcontainers.PostgreSql` `4.13.0` with Docker for isolated real-engine tests.
- **Alternatives considered:** Shared developer database; Compose-only integration tests; EF InMemory; mocked repositories.
- **Reason:** The PostgreSQL module is purpose-built for starting a disposable PostgreSQL container and version 4.13.0 is the current stable Testcontainers release. Isolation and disposal are necessary for repeatable local/CI evidence.
- **Version:** 4.13.0.
- **License:** MIT.
- **Source:** [Testcontainers.PostgreSql on NuGet](https://www.nuget.org/packages/Testcontainers.PostgreSql/4.13.0), [Testcontainers for .NET PostgreSQL module](https://dotnet.testcontainers.org/modules/postgres/).
- **Risk:** Docker daemon access is a hard prerequisite. Missing/unreachable Docker must fail the required integration gate explicitly, not convert the suite into a skip/PASS.

## Decision 7: Test runner

- **Decision:** Use xUnit v3 stable `3.2.2` with Microsoft Testing Platform and configure .NET 10 `global.json` test runner support deliberately.
- **Alternatives considered:** Keep xUnit 2.9.3 with VSTest; use the current xUnit 4 prerelease; change frameworks.
- **Reason:** The current xUnit package is deprecated in this environment. Official xUnit v3 guidance supports .NET 8+ and Microsoft Testing Platform through `dotnet test`; 3.2.2 is the current stable package while 4.0 is prerelease.
- **Version:** xunit.v3 3.2.2.
- **License:** Apache-2.0.
- **Source:** [xUnit v3 package](https://www.nuget.org/packages/xunit.v3/3.2.2), [xUnit v3 getting started](https://xunit.net/docs/getting-started/v3/getting-started), [Microsoft Testing Platform with xUnit v3](https://xunit.net/docs/getting-started/v3/microsoft-testing-platform).
- **Risk:** Migrating runner conventions can create “no tests found” false greens. The quality gate must assert expected test projects and nonzero executed test counts.

## Decision 8: Architecture tests

- **Decision:** Use `TngTech.ArchUnitNET` and its xUnit v3 integration `0.13.3`.
- **Alternatives considered:** Hand-written reflection tests; compile-time analyzers; the xUnit v2 integration.
- **Reason:** ArchUnitNET directly evaluates compiled architecture and publishes an xUnit v3 adapter at the same stable version. It can make the project direction and Elsa-source prohibition executable without inventing a local framework.
- **Version:** 0.13.3.
- **License:** Apache-2.0.
- **Source:** [ArchUnitNET](https://www.nuget.org/packages/TngTech.ArchUnitNET/0.13.3), [ArchUnitNET xUnit v3 adapter](https://www.nuget.org/packages/TngTech.ArchUnitNET.xUnitV3/0.13.3).
- **Risk:** Binary analysis can behave differently by build configuration. Run the documented configuration consistently and ensure production assemblies are referenced and loaded.

## Decision 9: Playwright and E2E

- **Decision:** Do not add Playwright or retain an empty E2E project during bootstrap. Report E2E as `NOT_APPLICABLE` until a real UI/product surface exists.
- **Alternatives considered:** Install Microsoft.Playwright now and keep a placeholder; claim API smoke as E2E.
- **Reason:** There is no real UI or browser journey, so Playwright would add a large browser/toolchain dependency with no meaningful acceptance scenario. The current stable package is recorded for future research refresh, not approved for current CPM.
- **Version:** Microsoft.Playwright 1.61.0 observed; deferred.
- **License:** MIT.
- **Source:** [Microsoft.Playwright on NuGet](https://www.nuget.org/packages/Microsoft.Playwright/1.61.0), [Playwright .NET documentation](https://playwright.dev/dotnet/).
- **Risk:** A future UI feature must explicitly activate E2E and reverify the current version/browser installation procedure.

## Decision 10: Gitleaks

- **Decision:** Use Gitleaks CLI `8.30.0`, verified by published checksum, and add a harmless canary detection. Do not use Gitleaks `8.30.1` or the separately licensed Gitleaks GitHub Action for the initial CI gate.
- **Alternatives considered:** Latest 8.30.1; Gitleaks Action; 8.28.0; secret scanning only through Trivy.
- **Reason:** 8.30.1 is the current release, but an upstream issue reports that default rules can return success for a canonical token and notes unusual release ancestry. 8.30.0 is the preceding signed stable release. The CLI is MIT, whereas the Action has separate organization-account license terms. A canary guards against future false-green scanner regressions.
- **Version:** 8.30.0.
- **License:** MIT for the CLI; Gitleaks Action not selected and has separate terms.
- **Source:** [Gitleaks releases](https://github.com/gitleaks/gitleaks/releases), [8.30.1 regression report](https://github.com/gitleaks/gitleaks/issues/2170), [CLI license](https://github.com/gitleaks/gitleaks/blob/master/LICENSE), [Action license](https://github.com/gitleaks/gitleaks-action/blob/master/LICENSE.txt).
- **Risk:** The regression is an open upstream report rather than a completed maintainer advisory. Pinning the preceding release plus a real canary is safer than trusting either version label alone.

## Decision 11: Trivy

- **Decision:** Use Trivy `0.72.0` with checksum/signature or immutable image digest verification. Pin any CI action to a reviewed full commit SHA; never use mutable tags or `latest`.
- **Alternatives considered:** 0.69.3 known-safe fallback; affected 0.69.4; Docker Hub 0.69.5/0.69.6; floating Trivy Action; use Grype alone.
- **Reason:** The upstream changelog records 0.72.0 as the June 2026 release. The March 2026 critical advisory documents malicious 0.69.4 artifacts, malicious Docker Hub 0.69.5/0.69.6 images, and force-pushed action tags, and explicitly recommends full-SHA action pinning. Trivy still adds configuration/container scanning not replaced by Grype's SBOM vulnerability role.
- **Version:** 0.72.0; affected versions explicitly denied.
- **License:** Apache-2.0.
- **Source:** [Trivy 0.72.0 release](https://github.com/aquasecurity/trivy/releases/tag/v0.72.0), [Trivy changelog](https://github.com/aquasecurity/trivy/blob/main/CHANGELOG.md), [critical supply-chain advisory](https://github.com/aquasecurity/trivy/security/advisories/GHSA-69fq-xp46-6x23), [license](https://github.com/aquasecurity/trivy/blob/main/LICENSE).
- **Risk:** Scanner acquisition itself is a supply-chain boundary. CI must verify immutable identity before execution and must not expose secrets to untrusted scanner setup code.

## Decision 12: Syft SBOM

- **Decision:** Use Syft `1.50.0` to generate CycloneDX JSON for the resolved application/container deliverable.
- **Alternatives considered:** Trivy-only SBOM; `dotnet list package` inventory; SPDX output only.
- **Reason:** Syft's current immutable release provides checksums, signatures, release attestation, and its own SBOM artifacts. CycloneDX JSON is machine-readable and can be consumed directly by the selected Grype gate. Package lists alone do not describe container/OS components.
- **Version:** 1.50.0.
- **License:** Apache-2.0.
- **Source:** [Syft releases](https://github.com/anchore/syft/releases), [Syft repository/license](https://github.com/anchore/syft).
- **Risk:** License discovery for NuGet dependencies can be incomplete from build assets alone. Keep registry-derived license evidence and THIRD_PARTY_NOTICES review in addition to the SBOM.

## Decision 13: Grype

- **Decision:** Use Grype `0.116.1` to scan the Syft SBOM under an explicit severity/fix policy.
- **Alternatives considered:** Trivy as the sole vulnerability scanner; omit the post-SBOM vulnerability check; use a paid service.
- **Reason:** The current immutable release includes published checksums/signatures/attestation. Scanning the produced SBOM proves it is consumable and separates inventory generation from vulnerability policy.
- **Version:** 0.116.1.
- **License:** Apache-2.0.
- **Source:** [Grype releases](https://github.com/anchore/grype/releases), [Grype repository/license](https://github.com/anchore/grype).
- **Risk:** Vulnerability database freshness affects results. Evidence must record DB status/time; network failure or an unavailable required DB is a failure, not a silent skip.

## Approved initial version and license matrix

| Component | Version | License | Bootstrap status |
|---|---:|---|---|
| .NET SDK/runtime | 10.0.302 / 10.0.10 | MIT plus bundled notices | Approved |
| Elsa | 3.7.1 | MIT | Approved |
| Aspire.Hosting.AppHost | 13.4.6 | MIT | Approved |
| Aspire.Hosting.PostgreSQL | 13.4.6 | MIT | Approved |
| PostgreSQL server | 18.4 | PostgreSQL License | Approved |
| Microsoft.EntityFrameworkCore | 10.0.10 | MIT | Approved |
| Npgsql.EntityFrameworkCore.PostgreSQL | 10.0.3 | PostgreSQL License | Approved |
| Testcontainers.PostgreSql | 4.13.0 | MIT | Approved |
| xunit.v3 | 3.2.2 | Apache-2.0 | Approved |
| TngTech.ArchUnitNET + xUnitV3 adapter | 0.13.3 | Apache-2.0 | Approved |
| Microsoft.Playwright | 1.61.0 | MIT | Deferred; no UI |
| Gitleaks CLI | 8.30.0 | MIT | Approved with canary |
| Trivy | 0.72.0 | Apache-2.0 | Approved with integrity pinning |
| Syft | 1.50.0 | Apache-2.0 | Approved |
| Grype | 0.116.1 | Apache-2.0 | Approved |

## Material plan changes from research

1. The SDK pin moves from the audited machine's 10.0.103 to the current security-serviced 10.0.302 feature band.
2. Elsa uses the official stable umbrella package for the first smoke rather than guessing a hand-selected internal package graph.
3. Tests migrate to xUnit v3/Microsoft Testing Platform rather than carrying the deprecated xUnit v2 package.
4. Playwright and the empty E2E project are removed/deferred because no real UI scenario exists.
5. Gitleaks is pinned to 8.30.0 with a canary instead of blindly selecting the latest 8.30.1 or the separately licensed Action.
6. Trivy acquisition/action references require immutable identity and integrity verification because the upstream ecosystem suffered a critical supply-chain compromise.
7. PostgreSQL is pinned to current 18.4 and container evidence must record a resolved digest rather than use `latest`.
