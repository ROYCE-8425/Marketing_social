using DXOS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DXOS.Unit.Tests;

[CollectionDefinition("EnvironmentVariables", DisableParallelization = true)]
public sealed class EnvironmentVariablesCollection
{
}

[Collection("EnvironmentVariables")]
public sealed class BootstrapDbContextFactoryTests
{
    private const string DummyConnectionString = "Host=localhost;Database=test_db;Username=test_user;Password=test_pass";

    [Fact]
    public void CreateDbContext_ThrowsInvalidOperationException_WhenNoConnectionStringConfigured()
    {
        var originalDxos = Environment.GetEnvironmentVariable("DXOS_CONNECTION_STRING");
        var originalDefault = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var originalDxosConn = Environment.GetEnvironmentVariable("ConnectionStrings__dxos");

        try
        {
            Environment.SetEnvironmentVariable("DXOS_CONNECTION_STRING", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__dxos", null);

            var factory = new BootstrapDbContextFactory();
            var ex = Assert.Throws<InvalidOperationException>(() => factory.CreateDbContext([]));

            Assert.Contains("Design-time connection string is required", ex.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("DXOS_CONNECTION_STRING", originalDxos);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", originalDefault);
            Environment.SetEnvironmentVariable("ConnectionStrings__dxos", originalDxosConn);
        }
    }

    [Fact]
    public void CreateDbContext_UsesExplicitArgument_WhenProvided()
    {
        var originalDxos = Environment.GetEnvironmentVariable("DXOS_CONNECTION_STRING");
        var originalDefault = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var originalDxosConn = Environment.GetEnvironmentVariable("ConnectionStrings__dxos");

        try
        {
            Environment.SetEnvironmentVariable("DXOS_CONNECTION_STRING", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__dxos", null);

            var factory = new BootstrapDbContextFactory();
            using var context = factory.CreateDbContext([DummyConnectionString]);

            Assert.NotNull(context);
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            Assert.Contains("Host=localhost", context.Database.GetConnectionString());
            Assert.Contains("Database=test_db", context.Database.GetConnectionString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DXOS_CONNECTION_STRING", originalDxos);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", originalDefault);
            Environment.SetEnvironmentVariable("ConnectionStrings__dxos", originalDxosConn);
        }
    }

    [Fact]
    public void CreateDbContext_RespectsEnvironmentPrecedence_DxosConnectionStringFirst()
    {
        var originalDxos = Environment.GetEnvironmentVariable("DXOS_CONNECTION_STRING");
        var originalDefault = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var originalDxosConn = Environment.GetEnvironmentVariable("ConnectionStrings__dxos");

        try
        {
            Environment.SetEnvironmentVariable("DXOS_CONNECTION_STRING", "Host=primary;Database=dxos;Username=u;Password=p");
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=secondary;Database=dxos;Username=u;Password=p");
            Environment.SetEnvironmentVariable("ConnectionStrings__dxos", "Host=tertiary;Database=dxos;Username=u;Password=p");

            var factory = new BootstrapDbContextFactory();
            using var context = factory.CreateDbContext([]);

            Assert.NotNull(context);
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            Assert.Contains("Host=primary", context.Database.GetConnectionString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DXOS_CONNECTION_STRING", originalDxos);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", originalDefault);
            Environment.SetEnvironmentVariable("ConnectionStrings__dxos", originalDxosConn);
        }
    }

    [Fact]
    public void CreateDbContext_RespectsEnvironmentPrecedence_DefaultConnectionSecond()
    {
        var originalDxos = Environment.GetEnvironmentVariable("DXOS_CONNECTION_STRING");
        var originalDefault = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        var originalDxosConn = Environment.GetEnvironmentVariable("ConnectionStrings__dxos");

        try
        {
            Environment.SetEnvironmentVariable("DXOS_CONNECTION_STRING", null);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Host=secondary;Database=dxos;Username=u;Password=p");
            Environment.SetEnvironmentVariable("ConnectionStrings__dxos", "Host=tertiary;Database=dxos;Username=u;Password=p");

            var factory = new BootstrapDbContextFactory();
            using var context = factory.CreateDbContext([]);

            Assert.NotNull(context);
            Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
            Assert.Contains("Host=secondary", context.Database.GetConnectionString());
        }
        finally
        {
            Environment.SetEnvironmentVariable("DXOS_CONNECTION_STRING", originalDxos);
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", originalDefault);
            Environment.SetEnvironmentVariable("ConnectionStrings__dxos", originalDxosConn);
        }
    }
}
