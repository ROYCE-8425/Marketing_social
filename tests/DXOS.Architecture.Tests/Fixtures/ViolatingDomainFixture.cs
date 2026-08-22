namespace DXOS.Architecture.Tests.Fixtures;

/// <summary>
/// Deliberate test-only fixture that violates the architecture rule by having a type depend on Infrastructure.
/// This type resides only in the test assembly and is never part of production assemblies.
/// </summary>
public sealed class ViolatingDomainClassThatDependsOnInfrastructure
{
    public DXOS.Infrastructure.Persistence.Entities.RuntimeProbe? DirectInfrastructureDependency { get; set; }
}
