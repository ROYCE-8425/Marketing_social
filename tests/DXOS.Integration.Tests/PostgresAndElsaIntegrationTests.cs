using System.Data.Common;
using DXOS.Infrastructure.Persistence;
using DXOS.Infrastructure.Persistence.Entities;
using DXOS.Integration.Tests.Teardown;
using DXOS.Workflows.Smoke;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Options;
using Elsa.Workflows.Runtime.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace DXOS.Integration.Tests;

public sealed class PostgresAndElsaIntegrationTests : IAsyncLifetime
{
    private const string PostgresImage = "postgres:18.4-alpine@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15";
    private readonly string _runId = Guid.NewGuid().ToString("N")[..12];
    private readonly PostgreSqlContainer _container;

    public PostgresAndElsaIntegrationTests()
    {
        var endpoint = Environment.GetEnvironmentVariable("DOCKER_HOST");
        var builder = new PostgreSqlBuilder(PostgresImage)
            .WithDatabase("dxos_integration_db")
            .WithUsername("dxos_integration_user")
            .WithPassword($"P@ss_{Guid.NewGuid():N}")
            .WithLabel("dxos.task", "open_source-cab.4")
            .WithLabel("dxos.test.run", _runId)
            .WithCleanUp(true);

        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            builder = builder.WithDockerEndpoint(endpoint);
        }

        _container = builder.Build();
    }

    private string? _containerId;

    public async ValueTask InitializeAsync()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        await _container.StartAsync(cts.Token);
        _containerId = _container.Id;
        Console.WriteLine($"[DXOS_INTEGRATION_TESTCONTAINER_INITIALIZED] ContainerId={_containerId} Task=open_source-cab.4 RunId={_runId}");
    }

    public async ValueTask DisposeAsync()
    {
        await ContainerTeardownHelper.TeardownAsync(
            ct => _container.StopAsync(ct),
            () => _container.DisposeAsync());

        Console.WriteLine($"[DXOS_INTEGRATION_TESTCONTAINER_DISPOSED] ContainerId={_containerId} Task=open_source-cab.4 RunId={_runId}");
    }

    [Fact]
    public async Task PostgreSql_MigrationsAndPersistence_SucceedsWithRealDatabase()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var connectionString = _container.GetConnectionString();

        var options = new DbContextOptionsBuilder<BootstrapDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        // 1. Apply real EF Core migrations
        using (var migrateContext = new BootstrapDbContext(options))
        {
            await migrateContext.Database.MigrateAsync(cts.Token);
        }

        // 2. Real readiness query against PostgreSQL
        using (var readyContext = new BootstrapDbContext(options))
        {
            var canConnect = await readyContext.Database.CanConnectAsync(cts.Token);
            Assert.True(canConnect, "PostgreSQL readiness check failed.");

            using var cmd = readyContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT 1;";
            await readyContext.Database.OpenConnectionAsync(cts.Token);
            var scalarResult = await cmd.ExecuteScalarAsync(cts.Token);
            Assert.Equal(1, Convert.ToInt32(scalarResult));
            await readyContext.Database.CloseConnectionAsync();
        }

        // 3. Prove runtime_probes table exists
        using (var checkContext = new BootstrapDbContext(options))
        {
            using var cmd = checkContext.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT EXISTS (SELECT FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'runtime_probes');";
            await checkContext.Database.OpenConnectionAsync(cts.Token);
            var exists = (bool)(await cmd.ExecuteScalarAsync(cts.Token) ?? false);
            Assert.True(exists, "Table runtime_probes must exist in public schema.");
            await checkContext.Database.CloseConnectionAsync();
        }

        // 4. Insert a real RuntimeProbe
        var probeId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var probe = new RuntimeProbe
        {
            Id = probeId,
            ProbeName = "integration-probe-" + _runId,
            Status = "PROBE_HEALTHY",
            CreatedAtUtc = createdAt
        };

        using (var insertContext = new BootstrapDbContext(options))
        {
            insertContext.RuntimeProbes.Add(probe);
            var affected = await insertContext.SaveChangesAsync(cts.Token);
            Assert.Equal(1, affected);
        }

        // 5. Read back from a fresh DbContext and verify all persisted values
        using (var readContext = new BootstrapDbContext(options))
        {
            var persisted = await readContext.RuntimeProbes.FindAsync([probeId], cts.Token);
            Assert.NotNull(persisted);
            Assert.Equal(probeId, persisted.Id);
            Assert.Equal("integration-probe-" + _runId, persisted.ProbeName);
            Assert.Equal("PROBE_HEALTHY", persisted.Status);
            Assert.Equal(createdAt.ToUnixTimeMilliseconds(), persisted.CreatedAtUtc.ToUnixTimeMilliseconds());
            Assert.True((persisted.CreatedAtUtc - createdAt).Duration() < TimeSpan.FromMilliseconds(100));
        }
    }

    [Fact]
    public async Task Elsa_EngineeringSmokeWorkflow_ExecutesDeterministically_WithTerminalStateAndOutput()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        var services = new ServiceCollection();
        services.AddElsa(elsa =>
        {
            elsa.AddWorkflow<EngineeringSmokeWorkflow>();
        });

        using var serviceProvider = services.BuildServiceProvider();
        var workflowRunner = serviceProvider.GetRequiredService<IWorkflowRunner>();

        var correlationId = $"corr-test-{_runId}";
        var input = new Dictionary<string, object>
        {
            [nameof(EngineeringSmokeWorkflow.CorrelationId)] = correlationId
        };

        var runOptions = new RunWorkflowOptions
        {
            CorrelationId = correlationId,
            Input = input
        };

        var result = await workflowRunner.RunAsync<EngineeringSmokeWorkflow>(runOptions, cancellationToken: cts.Token);

        Assert.NotNull(result);
        Assert.Equal(WorkflowStatus.Finished, result.WorkflowState.Status);
        Assert.Equal(WorkflowSubStatus.Finished, result.WorkflowState.SubStatus);

        Assert.True(result.WorkflowState.Output.TryGetValue("OutputResult", out var actualOutputObj));
        Assert.Equal(EngineeringSmokeWorkflow.ExpectedOutput, actualOutputObj?.ToString());

        Assert.True(result.WorkflowState.Output.TryGetValue("EchoedCorrelationId", out var actualCorrelationObj));
        Assert.Equal(correlationId, actualCorrelationObj?.ToString());
    }
}
