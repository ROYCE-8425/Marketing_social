using System.Reflection;
using ArchUnitNET.Domain;
using ArchUnitNET.Fluent;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnitV3;
using DXOS.Architecture.Tests.Fixtures;
using Xunit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

namespace DXOS.Architecture.Tests;

public sealed class ArchitectureTests
{
    private static readonly System.Reflection.Assembly DomainAssembly = typeof(DXOS.Domain.Campaign).Assembly;
    private static readonly System.Reflection.Assembly ApplicationAssembly = typeof(DXOS.Application.CampaignService).Assembly;
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(DXOS.Infrastructure.Persistence.BootstrapDbContext).Assembly;
    private static readonly System.Reflection.Assembly WorkflowsAssembly = typeof(DXOS.Workflows.Smoke.EngineeringSmokeWorkflow).Assembly;
    private static readonly System.Reflection.Assembly ApiAssembly = System.Reflection.Assembly.Load("DXOS.Api");
    private static readonly System.Reflection.Assembly AppHostAssembly = System.Reflection.Assembly.Load("DXOS.AppHost");

    private static readonly ArchUnitNET.Domain.Architecture ProductionArchitecture = new ArchLoader()
        .LoadAssemblies(
            DomainAssembly,
            ApplicationAssembly,
            InfrastructureAssembly,
            WorkflowsAssembly,
            ApiAssembly,
            AppHostAssembly)
        .Build();

    [Fact]
    public void Domain_MustNotDependOn_Application()
    {
        IArchRule rule = Types().That().ResideInAssembly(DomainAssembly)
            .Should().NotDependOnAny(Types().That().ResideInAssembly(ApplicationAssembly));

        rule.Check(ProductionArchitecture);
    }

    [Fact]
    public void Domain_MustNotDependOn_Infrastructure()
    {
        IArchRule rule = Types().That().ResideInAssembly(DomainAssembly)
            .Should().NotDependOnAny(Types().That().ResideInAssembly(InfrastructureAssembly));

        rule.Check(ProductionArchitecture);
    }

    [Fact]
    public void Domain_MustNotDependOn_Workflows()
    {
        IArchRule rule = Types().That().ResideInAssembly(DomainAssembly)
            .Should().NotDependOnAny(Types().That().ResideInAssembly(WorkflowsAssembly));

        rule.Check(ProductionArchitecture);
    }

    [Fact]
    public void Domain_MustNotDependOn_ApiOrAppHost()
    {
        IArchRule rule = Types().That().ResideInAssembly(DomainAssembly)
            .Should().NotDependOnAny(Types().That().ResideInAssembly(ApiAssembly).Or().ResideInAssembly(AppHostAssembly));

        rule.Check(ProductionArchitecture);
    }

    [Fact]
    public void Domain_MustNotReference_ProhibitedAssemblies()
    {
        var referenced = DomainAssembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
        var prohibitedPrefixes = new[] { "Elsa", "Microsoft.EntityFrameworkCore", "Npgsql", "Aspire", "Testcontainers" };

        foreach (var prefix in prohibitedPrefixes)
        {
            Assert.DoesNotContain(referenced, name => name != null && name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void Application_MayDependOnDomain_ButNotOnInfrastructureApiAppHostOrTestAssemblies()
    {
        IArchRule rule = Types().That().ResideInAssembly(ApplicationAssembly)
            .Should().NotDependOnAny(
                Types().That().ResideInAssembly(InfrastructureAssembly)
                    .Or().ResideInAssembly(ApiAssembly)
                    .Or().ResideInAssembly(AppHostAssembly));

        rule.Check(ProductionArchitecture);
    }

    [Fact]
    public void Infrastructure_MustNotDependOn_ApiAppHostOrTestAssemblies()
    {
        IArchRule rule = Types().That().ResideInAssembly(InfrastructureAssembly)
            .Should().NotDependOnAny(Types().That().ResideInAssembly(ApiAssembly).Or().ResideInAssembly(AppHostAssembly));

        rule.Check(ProductionArchitecture);
    }

    [Fact]
    public void Workflows_MustNotDependOn_ApiAppHostOrTestAssemblies()
    {
        IArchRule rule = Types().That().ResideInAssembly(WorkflowsAssembly)
            .Should().NotDependOnAny(Types().That().ResideInAssembly(ApiAssembly).Or().ResideInAssembly(AppHostAssembly));

        rule.Check(ProductionArchitecture);
    }

    [Fact]
    public void ProductionAssemblies_MustNotReferenceTestAssemblies()
    {
        var testAssemblyPrefixes = new[] { "DXOS.Unit.Tests", "DXOS.Architecture.Tests", "DXOS.Integration.Tests", "xunit", "ArchUnitNET", "Testcontainers" };
        var prodAssemblies = new[] { DomainAssembly, ApplicationAssembly, InfrastructureAssembly, WorkflowsAssembly, ApiAssembly, AppHostAssembly };

        foreach (var assembly in prodAssemblies)
        {
            var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name).ToList();
            foreach (var testPrefix in testAssemblyPrefixes)
            {
                Assert.DoesNotContain(referenced, r => r != null && r.StartsWith(testPrefix, StringComparison.OrdinalIgnoreCase));
            }
        }
    }

    [Fact]
    public void ProductionProjects_MustNotReferenceElsaSourceProjectsOrOldCheckout()
    {
        var testAssemblyPath = typeof(ArchitectureTests).Assembly.Location;
        var current = new System.IO.DirectoryInfo(System.IO.Path.GetDirectoryName(testAssemblyPath)!);
        while (current != null && !System.IO.File.Exists(System.IO.Path.Combine(current.FullName, "DXOS.slnx")))
        {
            current = current.Parent;
        }

        Assert.NotNull(current);
        var srcDir = System.IO.Path.Combine(current.FullName, "src");
        Assert.True(System.IO.Directory.Exists(srcDir), $"Source directory '{srcDir}' must exist.");

        var projectFiles = System.IO.Directory.GetFiles(srcDir, "*.csproj", System.IO.SearchOption.AllDirectories);
        Assert.NotEmpty(projectFiles);

        foreach (var projFile in projectFiles)
        {
            var xmlContent = System.IO.File.ReadAllText(projFile);
            var violations = ProjectFileBoundaryValidator.FindProhibitedProjectReferences(xmlContent);
            Assert.Empty(violations);
        }
    }

    [Fact]
    public void ProhibitedBootstrapPatterns_RemainAbsent()
    {
        var allTypes = ProductionArchitecture.Types;
        var violatingPatterns = allTypes
            .Where(t => t.Name.Contains("GenericRepository", StringComparison.OrdinalIgnoreCase) ||
                        t.Name.Contains("UnitOfWork", StringComparison.OrdinalIgnoreCase))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(violatingPatterns);
    }

    [Fact]
    public void ArchitectureRule_DetectsViolatingDomainFixture_WhenTypeDependsOnInfrastructure()
    {
        var fixtureArchitecture = new ArchLoader()
            .LoadAssemblies(typeof(ViolatingDomainClassThatDependsOnInfrastructure).Assembly, InfrastructureAssembly)
            .Build();

        IArchRule rule = Types().That().HaveName(nameof(ViolatingDomainClassThatDependsOnInfrastructure))
            .Should().NotDependOnAny(Types().That().ResideInAssembly(InfrastructureAssembly));

        var results = rule.Evaluate(fixtureArchitecture).ToList();

        Assert.NotEmpty(results);
        Assert.All(results, evaluationResult => Assert.False(evaluationResult.Passed, "Violating domain fixture must fail the architecture rule."));
    }

    [Fact]
    public void ArchitectureRule_DetectsViolatingProjectReferenceFixture_WhenProjectReferencesElsaSourceCheckout()
    {
        var violations = ProjectFileBoundaryValidator.FindProhibitedProjectReferences(ViolatingProjectReferenceFixture.ViolatingProjectXml);
        Assert.NotEmpty(violations);
        Assert.Contains(violations, v => v.Contains("elsa-core", StringComparison.OrdinalIgnoreCase));

        var validResults = ProjectFileBoundaryValidator.FindProhibitedProjectReferences(ViolatingProjectReferenceFixture.ValidProjectXml);
        Assert.Empty(validResults);
    }
}

public static class ProjectFileBoundaryValidator
{
    public static IReadOnlyList<string> FindProhibitedProjectReferences(string projectXml)
    {
        var doc = System.Xml.Linq.XDocument.Parse(projectXml);
        var references = doc.Descendants("ProjectReference")
            .Select(x => x.Attribute("Include")?.Value)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return references
            .Where(r => r!.Contains("elsa-core", StringComparison.OrdinalIgnoreCase) ||
                        r!.StartsWith("..\\..\\..\\", StringComparison.OrdinalIgnoreCase) ||
                        r!.StartsWith("../../../", StringComparison.OrdinalIgnoreCase))
            .Select(r => r!)
            .ToList();
    }
}
