# .NET Architecture Rules

- Target .NET 10 (`net10.0`) exclusively via `global.json` and `Directory.Build.props`.
- Follow modular monolith architecture with inward dependency flow: `AppHost` -> `Api` -> `Workflows` -> `Infrastructure` -> `Application` -> `Domain`.
- Domain layer must remain pure with zero dependencies on infrastructure, persistence, or external SDKs.
- Elsa workflow engine is consumed strictly through approved NuGet packages; no source project references are permitted.
- Prohibit generic repositories, UnitOfWork wrappers over EF Core, and empty `IService`/`Service` ceremony pairs.
- Enforce architectural rules automatically via ArchUnitNET in `tests/DXOS.Architecture.Tests`.