using DXOS.Api;
using DXOS.Application;
using DXOS.Infrastructure.Persistence;
using DXOS.Workflows.Smoke;
using Elsa.Extensions;
using Elsa.Workflows;
using Elsa.Workflows.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

// Connection String Resolution (Aspire, Compose, or Direct Environment / Configuration)
var connectionString = builder.Configuration.GetConnectionString("dxos")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? builder.Configuration["DATABASE_URL"];

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "PostgreSQL connection string is required. Configure ConnectionStrings:dxos, ConnectionStrings:DefaultConnection, or DATABASE_URL.");
}

builder.Services.AddDbContext<BootstrapDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<CampaignCopyStub>();
builder.Services.AddScoped<ICampaignStore, CampaignStore>();
builder.Services.AddScoped<ILeadStore, LeadStore>();
builder.Services.AddScoped<ITrafficStore, TrafficStore>();
builder.Services.AddScoped<ISpendProposalStore, SpendProposalStore>();
builder.Services.AddScoped<CampaignService>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddScoped<DemoSeedService>();
builder.Services.AddScoped<TrafficService>();
builder.Services.AddScoped<SpendProposalService>();

// Health Checks
builder.Services.AddHealthChecks()
    .AddCheck("liveness", () => HealthCheckResult.Healthy("Liveness probe OK"), tags: ["live"]);

// Elsa Workflows
builder.Services.AddElsa(elsa =>
{
    elsa.AddWorkflow<EngineeringSmokeWorkflow>();
    elsa.AddWorkflow<DXOS.Workflows.Traffic.TrafficIngestWorkflow>();
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Liveness endpoint (Does NOT depend on PostgreSQL)
app.MapGet("/health/live", () => Results.Ok(new
{
    status = "Healthy",
    mode = "Liveness",
    timestamp = DateTimeOffset.UtcNow
}));

// Readiness endpoint (Performs real PostgreSQL schema verification via DbContext)
app.MapGet("/health/ready", async (BootstrapDbContext db, ILogger<Program> logger, CancellationToken cancellationToken) =>
{
    try
    {
        // Execute real query on the migrated probe table to prove database and schema readiness
        var probeCount = await db.RuntimeProbes.AsNoTracking().CountAsync(cancellationToken);
        return Results.Ok(new
        {
            status = "Healthy",
            mode = "Readiness",
            database = "Connected",
            probeCount = probeCount,
            timestamp = DateTimeOffset.UtcNow
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Readiness check failed against database");
        return Results.Json(new
        {
            status = "Unhealthy",
            mode = "Readiness",
            database = "Unreachable",
            timestamp = DateTimeOffset.UtcNow
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

// Engineering Smoke Workflow Endpoint
app.MapPost("/smoke/workflow", async (
    HttpContext httpContext,
    IWorkflowRunner workflowRunner,
    BootstrapDbContext db,
    IConfiguration config,
    ILogger<Program> logger) =>
{
    var smokeEnabled = config.GetValue<bool>("EngineeringSmoke:Enabled", false);
    if (!smokeEnabled)
    {
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    // Verify DB connectivity as required runtime dependency
    try
    {
        await db.RuntimeProbes.AsNoTracking().CountAsync(httpContext.RequestAborted);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Workflow smoke rejected because database dependency is unhealthy");
        return Results.Json(new
        {
            status = "Failed",
            error = "Database dependency is unavailable for workflow smoke execution."
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    var correlationId = httpContext.Request.Query["correlationId"].ToString();
    if (string.IsNullOrWhiteSpace(correlationId))
    {
        correlationId = Guid.NewGuid().ToString("N");
    }

    var workflow = new EngineeringSmokeWorkflow();
    var runWorkflowOptions = new RunWorkflowOptions
    {
        CorrelationId = correlationId,
        Input = new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        }
    };

    // Bounded execution timeout
    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, httpContext.RequestAborted);

    var result = await workflowRunner.RunAsync(
        workflow,
        runWorkflowOptions,
        linkedCts.Token);

    var workflowState = result.WorkflowState;
    var status = workflowState.Status;
    var subStatus = workflowState.SubStatus;

    if (status != WorkflowStatus.Finished || subStatus != WorkflowSubStatus.Finished)
    {
        logger.LogError("Workflow ended in non-terminal or non-finished state: Status={Status}, SubStatus={SubStatus}", status, subStatus);
        return Results.Json(new
        {
            status = "Failed",
            workflowStatus = status.ToString(),
            workflowSubStatus = subStatus.ToString(),
            error = "Workflow did not reach terminal Finished state."
        }, statusCode: StatusCodes.Status500InternalServerError);
    }

    // Extract actual workflow outputs
    workflowState.Output.TryGetValue("OutputResult", out var outputVal);
    workflowState.Output.TryGetValue("EchoedCorrelationId", out var echoedCorrVal);

    var actualOutput = outputVal?.ToString();
    var echoedCorrelationId = echoedCorrVal?.ToString();

    if (actualOutput != EmitSmokeResultActivity.ExpectedOutput || echoedCorrelationId != correlationId)
    {
        logger.LogError("Workflow output verification failed: Output={Output}, EchoedCorr={EchoedCorr}, ExpectedCorr={ExpectedCorr}",
            actualOutput, echoedCorrelationId, correlationId);
        return Results.Json(new
        {
            status = "Failed",
            workflowStatus = status.ToString(),
            error = "Workflow output or correlation ID mismatch."
        }, statusCode: StatusCodes.Status500InternalServerError);
    }

    return Results.Ok(new
    {
        workflowInstanceId = workflowState.Id,
        workflowStatus = status.ToString(),
        workflowSubStatus = subStatus.ToString(),
        correlationId = correlationId,
        echoedCorrelationId = echoedCorrelationId,
        output = actualOutput
    });
});

app.MapMarketingSlice();

// Startup database migration if explicitly requested
if (app.Configuration.GetValue<bool>("Database:AutoMigrate", false))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<BootstrapDbContext>();
    // If migration fails, throw immediately to fail fast at startup
    await db.Database.MigrateAsync();
}

app.Run();
