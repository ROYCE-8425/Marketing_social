using DXOS.Application;
using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class LeadConversionTests
{
    private static readonly DateTimeOffset NowUtc = new(2026, 8, 22, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Convert_SalesRole_ValidRevenue_SucceedsAndSetsProperties()
    {
        var lead = Lead.Intake("Nguyen Van A", "0901234567", "a@example.com", LeadSource.Facebook, null, "sales-1", NowUtc);

        lead.Convert(ActorRole.Sales, "sales-1", 15_000_000m, NowUtc.AddMinutes(5));

        Assert.True(lead.IsConverted);
        Assert.Equal(NowUtc.AddMinutes(5), lead.ConvertedAtUtc);
        Assert.Equal(15_000_000m, lead.ConversionRevenueVnd);
    }

    [Fact]
    public void Convert_SystemRole_NullRevenue_Succeeds()
    {
        var lead = Lead.Intake("Tran Thi B", "0912345678", "b@example.com", LeadSource.TikTok, null, null, NowUtc);

        lead.Convert(ActorRole.System, "system-bot", null, NowUtc.AddMinutes(10));

        Assert.True(lead.IsConverted);
        Assert.Equal(NowUtc.AddMinutes(10), lead.ConvertedAtUtc);
        Assert.Null(lead.ConversionRevenueVnd);
    }

    [Fact]
    public void Convert_CalledTwice_ThrowsAlreadyConverted()
    {
        var lead = Lead.Intake("Le Van C", "0923456789", "c@example.com", LeadSource.Zalo, null, "sales-1", NowUtc);
        lead.Convert(ActorRole.Sales, "sales-1", 5_000_000m, NowUtc.AddMinutes(2));

        var ex = Assert.Throws<DomainRuleException>(() =>
            lead.Convert(ActorRole.Sales, "sales-1", 10_000_000m, NowUtc.AddMinutes(5)));

        Assert.Equal("AlreadyConverted", ex.Code);
    }

    [Theory]
    [InlineData(ActorRole.Marketer)]
    [InlineData(ActorRole.Content)]
    [InlineData(ActorRole.Owner)]
    public void Convert_NonSalesOrSystemRole_ThrowsForbiddenRole(ActorRole role)
    {
        var lead = Lead.Intake("Pham Van D", "0934567890", "d@example.com", LeadSource.Form, null, null, NowUtc);

        var ex = Assert.Throws<DomainRuleException>(() =>
            lead.Convert(role, "actor-1", 1_000_000m, NowUtc));

        Assert.Equal("ForbiddenRole", ex.Code);
    }

    [Fact]
    public void Convert_NegativeRevenue_ThrowsInvalidRevenue()
    {
        var lead = Lead.Intake("Hoang Van E", "0945678901", "e@example.com", LeadSource.Call, null, "sales-1", NowUtc);

        var ex = Assert.Throws<DomainRuleException>(() =>
            lead.Convert(ActorRole.Sales, "sales-1", -500_000m, NowUtc));

        Assert.Equal("InvalidRevenue", ex.Code);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Convert_EmptySalesActor_ThrowsInvalidActor(string? emptyActor)
    {
        var lead = Lead.Intake("Vu Van F", "0956789012", "f@example.com", LeadSource.Message, null, null, NowUtc);

        var ex = Assert.Throws<DomainRuleException>(() =>
            lead.Convert(ActorRole.Sales, emptyActor!, 1_000_000m, NowUtc));

        Assert.Equal("InvalidActor", ex.Code);
    }

    [Fact]
    public async Task Service_ConvertAsync_And_SummarizeByPlatformAsync_CorrectlyCalculatesConvertedAndRevenue()
    {
        var clock = new FixedClock(NowUtc);
        var store = new MemoryLeadStore(["sales-1", "sales-2"]);
        var service = new LeadService(store, clock);

        var fbLead = await service.IntakeFormAsync("Khach FB", "0901111222", "fb@example.com", null, CancellationToken.None);
        var ttLead = await service.RecordMessageOrCallAsync("Khach TT", "0903333444", "tt@example.com", LeadSource.Message, null, CancellationToken.None);

        var salesActor = new ActorContext(ActorRole.Sales, "sales-1");
        await service.ConvertAsync(salesActor, fbLead.Id, 25_000_000m, CancellationToken.None);

        var summary = await service.SummarizeByPlatformAsync(CancellationToken.None);

        var formSummary = Assert.Single(summary, s => s.Provider == "website");
        Assert.Equal(1, formSummary.LeadCount);
        Assert.Equal(1, formSummary.ConvertedCount);
        Assert.Equal(25_000_000m, formSummary.RevenueVnd);

        var messageSummary = Assert.Single(summary, s => s.Provider == "message");
        Assert.Equal(1, messageSummary.LeadCount);
        Assert.Equal(0, messageSummary.ConvertedCount);
        Assert.Equal(0m, messageSummary.RevenueVnd);
    }

    [Fact]
    public void Restore_WithConversionProperties_RestoresState()
    {
        var id = Guid.NewGuid();
        var convertedAt = NowUtc.AddDays(-1);
        var lead = Lead.Restore(
            id,
            "Old Lead",
            "0909999999",
            "old@example.com",
            LeadSource.Facebook,
            [LeadSource.Facebook, LeadSource.Zalo],
            85,
            LeadLabel.Hot,
            new ScoreBreakdown(40, 20, 20, 5, 0, 85),
            ["Reason 1"],
            null,
            "sales-1",
            NowUtc.AddDays(-2),
            "sales-1",
            NowUtc.AddDays(-2),
            [],
            null,
            NowUtc.AddDays(-3),
            NowUtc.AddDays(-1),
            "rules",
            "1.0",
            NowUtc.AddDays(-3),
            convertedAt,
            50_000_000m);

        Assert.True(lead.IsConverted);
        Assert.Equal(convertedAt, lead.ConvertedAtUtc);
        Assert.Equal(50_000_000m, lead.ConversionRevenueVnd);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class MemoryLeadStore : ILeadStore
    {
        private readonly List<Lead> _leads = [];
        private readonly List<string> _sales;
        private string? _lastAssigned;

        public MemoryLeadStore(IEnumerable<string> sales) => _sales = sales.ToList();

        public IReadOnlyList<Lead> Leads => _leads;

        public Task AddAsync(Lead lead, CancellationToken cancellationToken)
        {
            _leads.Add(lead);
            return Task.CompletedTask;
        }

        public Task<Lead?> GetAsync(Guid id, CancellationToken cancellationToken)
            => Task.FromResult(_leads.FirstOrDefault(l => l.Id == id));

        public Task<Lead?> FindByPhoneOrEmailAsync(string? phone, string? email, CancellationToken cancellationToken)
        {
            var found = _leads.FirstOrDefault(l =>
                (!string.IsNullOrWhiteSpace(phone) && l.Phone == phone) ||
                (!string.IsNullOrWhiteSpace(email) && l.Email == email));
            return Task.FromResult(found);
        }

        public Task<IReadOnlyList<Lead>> ListAsync(CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyList<Lead>)_leads.ToList());

        public Task UpdateAsync(Lead lead, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<int> CountAsync(CancellationToken cancellationToken) => Task.FromResult(_leads.Count);

        public Task<IReadOnlyList<string>> ListSalesActorsAsync(CancellationToken cancellationToken)
            => Task.FromResult((IReadOnlyList<string>)_sales);

        public Task RememberSalesActorAsync(string actorId, CancellationToken cancellationToken)
        {
            if (!_sales.Contains(actorId, StringComparer.Ordinal))
            {
                _sales.Add(actorId);
            }

            return Task.CompletedTask;
        }

        public Task<string?> GetLastAssignedSalesActorAsync(CancellationToken cancellationToken)
            => Task.FromResult(_lastAssigned);

        public Task SetLastAssignedSalesActorAsync(string actorId, CancellationToken cancellationToken)
        {
            _lastAssigned = actorId;
            return Task.CompletedTask;
        }
    }
}
