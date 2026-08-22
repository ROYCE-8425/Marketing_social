# Verification Report: BR001-R2

## Repository Identity
| Metric | Value |
|---|---|
| Working Directory | `C:\Users\199X\OneDrive\Máy tính\olympic\dx-os` |
| HEAD Commit | 26f10dc031cbea23f63fe16d94cc2b99f63c9427 |
| Branch | `main` |
| Remote | none |
| Git Status | 8 paths modified |

## Modified Paths
- `.beads/interactions.jsonl`
- `artifacts/task-runs/open_source-cab.2/implementation-report.md`
- `artifacts/task-runs/open_source-cab.2/review.md`
- `artifacts/task-runs/open_source-cab.2/verification-output.sha256`
- `artifacts/task-runs/open_source-cab.2/verification-output.txt`
- `artifacts/task-runs/open_source-cab.2/verification.md`
- `artifacts/task-runs/open_source-cab.2/verify-r2.ps1`
- `openspec/changes/bootstrap-remediation-001/tasks.md`

## OpenSpec and Beads Results
| Check | Status |
|---|---|
| OpenSpec Validation | Passed |
| Beads Issue Status | `in_progress` |
| OpenSpec Tasks | R2.1 - R2.4 unchecked for review |

## Command Matrix
| Command | Exit Code | Duration | Result |
|---|---|---|---|
| `dotnet --version` | 0 | (see transcript) | `10.0.302` |
| `dotnet restore DXOS.slnx --locked-mode` | 0 | (see transcript) | 0 errors |
| `dotnet build DXOS.slnx -c Release --no-restore -warnaserror` | 0 | (see transcript) | 0 warnings, 0 errors |

## Exact Project Graph
- **DXOS.Domain**: none
- **DXOS.Application**: -> DXOS.Domain
- **DXOS.Workflows**: -> DXOS.Application
- **DXOS.Infrastructure**: -> DXOS.Application, DXOS.Domain
- **DXOS.Api**: -> DXOS.Application, DXOS.Infrastructure, DXOS.Workflows
- **DXOS.AppHost**: -> DXOS.Api
- **Test Projects** (each test project): -> DXOS.Domain, DXOS.Application, DXOS.Workflows, DXOS.Api, DXOS.Infrastructure

## Evidence Identity
| File | Size (bytes) | SHA-256 Hash |
|---|---|---|
| `verification-output.txt` | EXTERNAL_FINAL_VERIFICATION | EXTERNAL_FINAL_VERIFICATION |
| `verify-r2.ps1` | 26106 | 42B63AA451EEFC5D16EC25665535C9C42B4B66F0BE05354F66A61028834DB137 |
| `run-r2-verification.ps1` | 5292 | 6DFB7F874134C1FCEE44ADE58186F3B09BD2A5BBC0F798FCDC07929FAD652639 |
| `verification-output.sha256` | 64 | EXTERNAL_FINAL_VERIFICATION |

**Policy**:
- transcript size/hash = EXTERNAL_FINAL_VERIFICATION
- `verification-output.sha256` is the authoritative sidecar
- Codex will compare the final transcript mechanically against the sidecar

## Package and License Inventory
| Package | Version | Consuming Projects | License | NuSpec Path |
|---|---|---|---|---|
| Microsoft.ApplicationInsights | 2.23.0 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.applicationinsights\2.23.0\microsoft.applicationinsights.nuspec` |
| Microsoft.Bcl.AsyncInterfaces | 6.0.0 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.bcl.asyncinterfaces\6.0.0\microsoft.bcl.asyncinterfaces.nuspec` |
| Microsoft.CodeCoverage | 17.13.0 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.codecoverage\17.13.0\microsoft.codecoverage.nuspec` |
| Microsoft.NET.Test.Sdk | 17.13.0 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.net.test.sdk\17.13.0\microsoft.net.test.sdk.nuspec` |
| Microsoft.Testing.Extensions.Telemetry | 1.9.1 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.testing.extensions.telemetry\1.9.1\microsoft.testing.extensions.telemetry.nuspec` |
| Microsoft.Testing.Extensions.TrxReport.Abstractions | 1.9.1 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.testing.extensions.trxreport.abstractions\1.9.1\microsoft.testing.extensions.trxreport.abstractions.nuspec` |
| Microsoft.Testing.Platform.MSBuild | 1.9.1 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.testing.platform.msbuild\1.9.1\microsoft.testing.platform.msbuild.nuspec` |
| Microsoft.Testing.Platform | 1.9.1 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.testing.platform\1.9.1\microsoft.testing.platform.nuspec` |
| Microsoft.TestPlatform.ObjectModel | 17.13.0 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.testplatform.objectmodel\17.13.0\microsoft.testplatform.objectmodel.nuspec` |
| Microsoft.TestPlatform.TestHost | 17.13.0 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.testplatform.testhost\17.13.0\microsoft.testplatform.testhost.nuspec` |
| Microsoft.Win32.Registry | 5.0.0 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\microsoft.win32.registry\5.0.0\microsoft.win32.registry.nuspec` |
| Newtonsoft.Json | 13.0.1 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | MIT | `C:\Users\199X\.nuget\packages\newtonsoft.json\13.0.1\newtonsoft.json.nuspec` |
| xunit.analyzers | 1.27.0 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | Apache-2.0 | `C:\Users\199X\.nuget\packages\xunit.analyzers\1.27.0\xunit.analyzers.nuspec` |
| xunit.v3.assert | 3.2.2 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | Apache-2.0 | `C:\Users\199X\.nuget\packages\xunit.v3.assert\3.2.2\xunit.v3.assert.nuspec` |
| xunit.v3.common | 3.2.2 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | Apache-2.0 | `C:\Users\199X\.nuget\packages\xunit.v3.common\3.2.2\xunit.v3.common.nuspec` |
| xunit.v3.core.mtp-v1 | 3.2.2 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | Apache-2.0 | `C:\Users\199X\.nuget\packages\xunit.v3.core.mtp-v1\3.2.2\xunit.v3.core.mtp-v1.nuspec` |
| xunit.v3.extensibility.core | 3.2.2 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | Apache-2.0 | `C:\Users\199X\.nuget\packages\xunit.v3.extensibility.core\3.2.2\xunit.v3.extensibility.core.nuspec` |
| xunit.v3.mtp-v1 | 3.2.2 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | Apache-2.0 | `C:\Users\199X\.nuget\packages\xunit.v3.mtp-v1\3.2.2\xunit.v3.mtp-v1.nuspec` |
| xunit.v3.runner.common | 3.2.2 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | Apache-2.0 | `C:\Users\199X\.nuget\packages\xunit.v3.runner.common\3.2.2\xunit.v3.runner.common.nuspec` |
| xunit.v3.runner.inproc.console | 3.2.2 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | Apache-2.0 | `C:\Users\199X\.nuget\packages\xunit.v3.runner.inproc.console\3.2.2\xunit.v3.runner.inproc.console.nuspec` |
| xunit.v3 | 3.2.2 | DXOS.Architecture.Tests,DXOS.Integration.Tests,DXOS.Unit.Tests | Apache-2.0 | `C:\Users\199X\.nuget\packages\xunit.v3\3.2.2\xunit.v3.nuspec` |

## Lock Evidence

| File | Before Size | Before Hash | After Size | After Hash | Ignored | Unchanged |
|---|---|---|---|---|---|---|
| `src\DXOS.Api\packages.lock.json` | 611 | 55D7A1FDC6BF3628B36B526DCEEFB8B9FEACC07E73F5071869E996AB56D65345 | 611 | 55D7A1FDC6BF3628B36B526DCEEFB8B9FEACC07E73F5071869E996AB56D65345 | False | True |
| `src\DXOS.AppHost\packages.lock.json` | 840 | AA3E9708B4503A72FD03DBE1058F3300C3241BDD7379409FDBD0066EE1F4AEBF | 840 | AA3E9708B4503A72FD03DBE1058F3300C3241BDD7379409FDBD0066EE1F4AEBF | False | True |
| `src\DXOS.Application\packages.lock.json` | 132 | 956C49D24E8E0917F84962D175E47BDA2265302ED0F05128E88E32880661C25D | 132 | 956C49D24E8E0917F84962D175E47BDA2265302ED0F05128E88E32880661C25D | False | True |
| `src\DXOS.Domain\packages.lock.json` | 66 | 2241481B7E74DCDCD600303146B730ECC12CBD8E49AA55755EF95EE499FE229A | 66 | 2241481B7E74DCDCD600303146B730ECC12CBD8E49AA55755EF95EE499FE229A | False | True |
| `src\DXOS.Infrastructure\packages.lock.json` | 275 | 6AC8F374F7F9690B48A68A6263F68547D72FA7BC9B97BD687017F222AB16AEB4 | 275 | 6AC8F374F7F9690B48A68A6263F68547D72FA7BC9B97BD687017F222AB16AEB4 | False | True |
| `src\DXOS.Workflows\packages.lock.json` | 275 | 6AC8F374F7F9690B48A68A6263F68547D72FA7BC9B97BD687017F222AB16AEB4 | 275 | 6AC8F374F7F9690B48A68A6263F68547D72FA7BC9B97BD687017F222AB16AEB4 | False | True |
| `tests\DXOS.Architecture.Tests\packages.lock.json` | 7289 | 6D42EE182E28DE4296C0389BFF2D97D6AA9276EAD214A326C13DF12836B8570E | 7289 | 6D42EE182E28DE4296C0389BFF2D97D6AA9276EAD214A326C13DF12836B8570E | False | True |
| `tests\DXOS.Integration.Tests\packages.lock.json` | 7289 | 6D42EE182E28DE4296C0389BFF2D97D6AA9276EAD214A326C13DF12836B8570E | 7289 | 6D42EE182E28DE4296C0389BFF2D97D6AA9276EAD214A326C13DF12836B8570E | False | True |
| `tests\DXOS.Unit.Tests\packages.lock.json` | 7289 | 6D42EE182E28DE4296C0389BFF2D97D6AA9276EAD214A326C13DF12836B8570E | 7289 | 6D42EE182E28DE4296C0389BFF2D97D6AA9276EAD214A326C13DF12836B8570E | False | True |
