using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DXOS.Infrastructure.Persistence;

public sealed class BootstrapDbContextFactory : IDesignTimeDbContextFactory<BootstrapDbContext>
{
    public BootstrapDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("DXOS_CONNECTION_STRING")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("ConnectionStrings__dxos");

        if (string.IsNullOrWhiteSpace(connectionString) && args.Length > 0 && !string.IsNullOrWhiteSpace(args[0]))
        {
            connectionString = args[0];
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Design-time connection string is required. Configure environment variable DXOS_CONNECTION_STRING, ConnectionStrings__DefaultConnection, or pass connection argument.");
        }

        var optionsBuilder = new DbContextOptionsBuilder<BootstrapDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new BootstrapDbContext(optionsBuilder.Options);
    }
}
