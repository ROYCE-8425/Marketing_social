namespace DXOS.Architecture.Tests.Fixtures;

/// <summary>
/// Deliberate test fixture containing a prohibited project reference to an Elsa source checkout.
/// Used to verify sensitivity of the source/path boundary architecture validator.
/// </summary>
public static class ViolatingProjectReferenceFixture
{
    public const string ViolatingProjectXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\..\..\elsa-core\src\modules\Elsa.Workflows.Core\Elsa.Workflows.Core.csproj"" />
  </ItemGroup>
</Project>";

    public const string ValidProjectXml = @"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include=""..\DXOS.Domain\DXOS.Domain.csproj"" />
  </ItemGroup>
</Project>";
}
