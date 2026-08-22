using System.Text.Json;
using DXOS.Application;
using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class McpServiceTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

    private sealed class FakeClock : IClock
    {
        public DateTimeOffset UtcNow => NowUtc;
    }

    private sealed class FakeLeadStore : ILeadStore
    {
        public List<Lead> Leads { get; } = [];
        public List<string> SalesActors { get; } = ["sales-1", "sales-2"];
        public string? LastAssigned { get; set; }

        public Task AddAsync(Lead lead, CancellationToken cancellationToken)
        {
            Leads.Add(lead);
            return Task.CompletedTask;
        }

        public Task<Lead?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(Leads.FirstOrDefault(l => l.Id == id));
        }

        public Task<Lead?> FindByPhoneOrEmailAsync(string? phone, string? email, CancellationToken cancellationToken)
        {
            return Task.FromResult(Leads.FirstOrDefault(l =>
                (!string.IsNullOrWhiteSpace(phone) && l.Phone == phone) ||
                (!string.IsNullOrWhiteSpace(email) && l.Email == email)));
        }

        public Task<IReadOnlyList<Lead>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Lead>>(Leads.ToList());
        }

        public Task UpdateAsync(Lead lead, CancellationToken cancellationToken)
        {
            var idx = Leads.FindIndex(l => l.Id == lead.Id);
            if (idx >= 0) Leads[idx] = lead;
            return Task.CompletedTask;
        }

        public Task<int> CountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(Leads.Count);
        }

        public Task<IReadOnlyList<string>> ListSalesActorsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(SalesActors);
        }

        public Task RememberSalesActorAsync(string actorId, CancellationToken cancellationToken)
        {
            if (!SalesActors.Contains(actorId)) SalesActors.Add(actorId);
            return Task.CompletedTask;
        }

        public Task<string?> GetLastAssignedSalesActorAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(LastAssigned);
        }

        public Task SetLastAssignedSalesActorAsync(string actorId, CancellationToken cancellationToken)
        {
            LastAssigned = actorId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWebhookStore : IWebhookEventStore
    {
        public DateTimeOffset? LastReceivedAtUtc { get; set; }

        public Task<WebhookBeginResult> TryBeginAsync(string provider, string externalEventId, string payloadHash, CancellationToken cancellationToken)
        {
            return Task.FromResult(new WebhookBeginResult(false, Guid.NewGuid(), null));
        }

        public Task CompleteAsync(Guid webhookEventId, Guid leadId, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<DateTimeOffset?> GetLastReceivedAtUtcAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(LastReceivedAtUtc);
        }
    }

    private static McpService CreateService(FakeLeadStore store, FakeWebhookStore? webhookStore = null)
    {
        var clock = new FakeClock();
        var leadService = new LeadService(store, clock, webhookStore);
        return new McpService(leadService, clock, trafficService: null, webhookStore: webhookStore);
    }

    [Fact]
    public void GetToolDefinitions_ReturnsAllSixExpectedTools()
    {
        var tools = McpService.GetToolDefinitions();

        Assert.Equal(6, tools.Count);
        var names = tools.Select(t => t.Name).ToList();
        Assert.Contains("lead_search", names);
        Assert.Contains("lead_get", names);
        Assert.Contains("lead_assign", names);
        Assert.Contains("analytics_summary", names);
        Assert.Contains("platform_connections", names);
        Assert.Contains("sync_status", names);
    }

    [Fact]
    public async Task ExecuteToolAsync_MissingActor_ThrowsDomainRuleException()
    {
        var store = new FakeLeadStore();
        var mcp = CreateService(store);
        var actor = new ActorContext(ActorRole.Sales, "");

        var ex = await Assert.ThrowsAsync<DomainRuleException>(() =>
            mcp.ExecuteToolAsync(actor, "lead_search", null, CancellationToken.None));

        Assert.Equal("InvalidActor", ex.Code);
    }

    [Fact]
    public async Task LeadSearch_FindsMatchingLeadsByQueryAndLabel()
    {
        var store = new FakeLeadStore();
        var lead1 = Lead.Intake("Nguyen Van A", "0901234567", "a@example.com", LeadSource.Facebook, null, "sales-1", NowUtc);
        var lead2 = Lead.Intake("Tran Thi B", "0912345678", "b@example.com", LeadSource.TikTok, null, null, NowUtc);
        store.Leads.Add(lead1);
        store.Leads.Add(lead2);

        var mcp = CreateService(store);
        var actor = new ActorContext(ActorRole.Sales, "sales-1");

        var args = JsonDocument.Parse("{\"query\":\"Nguyen\"}").RootElement;
        var result = await mcp.ExecuteToolAsync(actor, "lead_search", args, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(result.Content);
        var json = JsonSerializer.Serialize(result.Content);
        Assert.Contains("Nguyen Van A", json);
        Assert.DoesNotContain("Tran Thi B", json);
    }

    [Fact]
    public async Task LeadGet_ExistingLead_ReturnsLeadDetails()
    {
        var store = new FakeLeadStore();
        var lead = Lead.Intake("Le Van C", "0923456789", "c@example.com", LeadSource.Zalo, null, "sales-1", NowUtc);
        store.Leads.Add(lead);

        var mcp = CreateService(store);
        var actor = new ActorContext(ActorRole.Sales, "sales-1");

        var args = JsonDocument.Parse($"{{\"id\":\"{lead.Id}\"}}").RootElement;
        var result = await mcp.ExecuteToolAsync(actor, "lead_get", args, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(result.Content);
        var json = JsonSerializer.Serialize(result.Content);
        Assert.Contains(lead.Id.ToString(), json);
        Assert.Contains("Le Van C", json);
    }

    [Fact]
    public async Task LeadGet_NonExistentLead_ReturnsError()
    {
        var store = new FakeLeadStore();
        var mcp = CreateService(store);
        var actor = new ActorContext(ActorRole.Sales, "sales-1");

        var nonExistentId = Guid.NewGuid();
        var args = JsonDocument.Parse($"{{\"id\":\"{nonExistentId}\"}}").RootElement;
        var result = await mcp.ExecuteToolAsync(actor, "lead_get", args, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("was not found", result.Error);
    }

    [Fact]
    public async Task LeadAssign_SalesRole_ClaimsLead()
    {
        var store = new FakeLeadStore();
        var lead = Lead.Intake("Pham Thi D", "0934567890", "d@example.com", LeadSource.Form, null, "sales-1", NowUtc);
        store.Leads.Add(lead);

        var mcp = CreateService(store);
        var actor = new ActorContext(ActorRole.Sales, "sales-1");

        var args = JsonDocument.Parse($"{{\"leadId\":\"{lead.Id}\"}}").RootElement;
        var result = await mcp.ExecuteToolAsync(actor, "lead_assign", args, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(result.Content);
        Assert.Equal("sales-1", lead.ClaimedByActor);
    }

    [Fact]
    public async Task LeadAssign_MarketerRole_ReturnsForbiddenRoleError()
    {
        var store = new FakeLeadStore();
        var lead = Lead.Intake("Vu Van F", "0956789012", "f@example.com", LeadSource.Form, null, "sales-1", NowUtc);
        store.Leads.Add(lead);

        var mcp = CreateService(store);
        var actor = new ActorContext(ActorRole.Marketer, "mai");

        var args = JsonDocument.Parse($"{{\"leadId\":\"{lead.Id}\"}}").RootElement;
        var result = await mcp.ExecuteToolAsync(actor, "lead_assign", args, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("ForbiddenRole", result.Error);
    }

    [Fact]
    public async Task AnalyticsSummary_ReturnsAggregatesAndNeverContainsTokens()
    {
        var store = new FakeLeadStore();
        var lead = Lead.Intake("Hoang Van E", "0945678901", "e@example.com", LeadSource.Facebook, null, "sales-1", NowUtc);
        store.Leads.Add(lead);

        var mcp = CreateService(store);
        var actor = new ActorContext(ActorRole.Marketer, "mai");

        var result = await mcp.ExecuteToolAsync(actor, "analytics_summary", null, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(result.Content);
        var json = JsonSerializer.Serialize(result.Content);
        Assert.Contains("platforms", json);
        Assert.Contains("cpl", json);
        Assert.DoesNotContain("access_token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task PlatformConnections_ReturnsConnectionsWithoutTokens()
    {
        var store = new FakeLeadStore();
        var mcp = CreateService(store);
        var actor = new ActorContext(ActorRole.Marketer, "mai");

        var result = await mcp.ExecuteToolAsync(actor, "platform_connections", null, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(result.Content);
        var json = JsonSerializer.Serialize(result.Content);
        Assert.Contains("facebook", json);
        Assert.Contains("tiktok", json);
        Assert.Contains("zalo", json);
        Assert.DoesNotContain("access_token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SyncStatus_ReturnsMetadataAndLastWebhookTimestamp()
    {
        var store = new FakeLeadStore();
        var webhookStore = new FakeWebhookStore { LastReceivedAtUtc = NowUtc.AddMinutes(-5) };
        var mcp = CreateService(store, webhookStore);
        var actor = new ActorContext(ActorRole.Marketer, "mai");

        var result = await mcp.ExecuteToolAsync(actor, "sync_status", null, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotNull(result.Content);
        var json = JsonSerializer.Serialize(result.Content);
        Assert.Contains("DXOS.Mcp", json);
        Assert.Contains("demo", json);
        Assert.Contains("lastEventReceivedAtUtc", json);
    }

    [Fact]
    public async Task HandleJsonRpcAsync_InitializeAndListTools_ReturnsValidRpcResponse()
    {
        var store = new FakeLeadStore();
        var mcp = CreateService(store);
        var actor = new ActorContext(ActorRole.Sales, "sales-1");

        var initReq = JsonDocument.Parse("{\"jsonrpc\":\"2.0\",\"id\":\"1\",\"method\":\"initialize\"}").RootElement;
        var initRes = await mcp.HandleJsonRpcAsync(actor, initReq, CancellationToken.None);
        var initJson = JsonSerializer.Serialize(initRes);
        Assert.Contains("protocolVersion", initJson);
        Assert.Contains("DXOS.Mcp", initJson);

        var listReq = JsonDocument.Parse("{\"jsonrpc\":\"2.0\",\"id\":\"2\",\"method\":\"tools/list\"}").RootElement;
        var listRes = await mcp.HandleJsonRpcAsync(actor, listReq, CancellationToken.None);
        var listJson = JsonSerializer.Serialize(listRes);
        Assert.Contains("lead_search", listJson);
        Assert.Contains("lead_get", listJson);
    }

    [Fact]
    public async Task HandleJsonRpcAsync_ToolsCall_ExecutesSuccessfully()
    {
        var store = new FakeLeadStore();
        var mcp = CreateService(store);
        var actor = new ActorContext(ActorRole.Sales, "sales-1");

        var callReq = JsonDocument.Parse("{\"jsonrpc\":\"2.0\",\"id\":\"3\",\"method\":\"tools/call\",\"params\":{\"name\":\"platform_connections\",\"arguments\":{}}}").RootElement;
        var callRes = await mcp.HandleJsonRpcAsync(actor, callReq, CancellationToken.None);
        var callJson = JsonSerializer.Serialize(callRes);
        Assert.Contains("facebook", callJson);
        Assert.Contains("tiktok", callJson);
    }
}
