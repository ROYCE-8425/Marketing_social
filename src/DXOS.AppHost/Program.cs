// RuntimeIdentifiers lists both win-x64 and linux-x64 for the lock file. The AppHost SDK
// then resolves DCP from the first RID (win-x64), which does not exist on Ubuntu Actions.
UseHostRidAspireTools();

var builder = DistributedApplication.CreateBuilder(args);

var runId = Environment.GetEnvironmentVariable("DXOS_RUN_ID");
if (!string.IsNullOrWhiteSpace(runId) && !System.Text.RegularExpressions.Regex.IsMatch(runId, "^[a-zA-Z0-9_-]{8,64}$"))
{
    throw new InvalidOperationException($"Invalid DXOS_RUN_ID format: '{runId}'. Expected alphanumeric string of 8-64 characters.");
}

var postgres = builder.AddPostgres("postgres")
    .WithImage("postgres")
    .WithImageTag("18.4-alpine@sha256:9a8afca54e7861fd90fab5fdf4c42477a6b1cb7d293595148e674e0a3181de15");

if (!string.IsNullOrWhiteSpace(runId))
{
    postgres.WithContainerRuntimeArgs("--label", $"dxos.run.id={runId}");
}

var db = postgres.AddDatabase("dxos");

var apiPortStr = Environment.GetEnvironmentVariable("API_PORT");
int? apiPort = int.TryParse(apiPortStr, out var p) && p > 0 ? p : null;

var api = builder.AddProject<Projects.DXOS_Api>("api")
    .WithReference(db)
    .WaitFor(db)
    .WithEnvironment("Database__AutoMigrate", "true")
    .WithEnvironment("EngineeringSmoke__Enabled", "true");

if (!string.IsNullOrWhiteSpace(runId))
{
    api.WithEnvironment("DXOS_RUN_ID", runId);
}

if (apiPort.HasValue)
{
    var portStr = apiPort.Value.ToString();
    api.WithHttpEndpoint(port: apiPort.Value, targetPort: apiPort.Value, name: "http", isProxied: false);
    // Callback wins over DCP's localhost/::1 injection. Smoke probes 127.0.0.1 on Ubuntu Actions.
    api.WithEnvironment(context =>
    {
        context.EnvironmentVariables["ASPNETCORE_URLS"] = "http://127.0.0.1:" + portStr;
    });
}

builder.Build().Run();

static void UseHostRidAspireTools()
{
    var rid = OperatingSystem.IsWindows()
        ? "win-x64"
        : "linux-x64";
    var dcpFile = OperatingSystem.IsWindows() ? "dcp.exe" : "dcp";
    var dashboardFile = OperatingSystem.IsWindows() ? "AspireDashboard.exe" : "AspireDashboard";

    var dcpPath = FindAspireTool("aspire.hosting.orchestration." + rid, dcpFile);
    if (!string.IsNullOrWhiteSpace(dcpPath) && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DcpPublisher__CliPath")))
    {
        Environment.SetEnvironmentVariable("DcpPublisher__CliPath", dcpPath);
    }

    var dashboardPath = FindAspireTool("aspire.dashboard.sdk." + rid, dashboardFile);
    if (!string.IsNullOrWhiteSpace(dashboardPath) && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DcpPublisher__DashboardPath")))
    {
        Environment.SetEnvironmentVariable("DcpPublisher__DashboardPath", dashboardPath);
    }
}

static string? FindAspireTool(string packageId, string fileName)
{
    var packagesRoot = Environment.GetEnvironmentVariable("NUGET_PACKAGES");
    if (string.IsNullOrWhiteSpace(packagesRoot))
    {
        packagesRoot = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".nuget", "packages");
    }

    var packageRoot = Path.Combine(packagesRoot, packageId);
    if (!Directory.Exists(packageRoot))
    {
        return null;
    }

    foreach (var versionDir in Directory.GetDirectories(packageRoot).OrderByDescending(static dir => dir, StringComparer.OrdinalIgnoreCase))
    {
        var candidate = Path.Combine(versionDir, "tools", fileName);
        if (File.Exists(candidate))
        {
            return candidate;
        }
    }

    return null;
}
