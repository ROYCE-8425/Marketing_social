using DXOS.Application;
using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class LeadSlaTests
{
    private static readonly DateTimeOffset T0 = new(2026, 8, 21, 3, 0, 0, TimeSpan.Zero); // 10:00 AM VN

    [Fact]
    public void Sla_Durations_HotIs5Min_WarmIs30Min()
    {
        Assert.Equal(TimeSpan.FromMinutes(5), LeadSla.HotDuration);
        Assert.Equal(TimeSpan.FromMinutes(30), LeadSla.WarmDuration);
        Assert.Null(LeadSla.GetDuration(LeadLabel.Cold));
        Assert.Null(LeadSla.GetDuration(LeadLabel.Junk));
    }

    [Fact]
    public void Sla_HotExpiresAfter5Minutes()
    {
        Assert.False(LeadSla.IsExpired(T0, T0.AddMinutes(4), LeadLabel.Hot));
        Assert.True(LeadSla.IsExpired(T0, T0.AddMinutes(5), LeadLabel.Hot));
    }

    [Fact]
    public void Sla_WarmExpiresAfter30Minutes()
    {
        Assert.False(LeadSla.IsExpired(T0, T0.AddMinutes(29), LeadLabel.Warm));
        Assert.True(LeadSla.IsExpired(T0, T0.AddMinutes(30), LeadLabel.Warm));
    }

    [Fact]
    public void Claim_Unclaims_WhenSlaExpires()
    {
        var lead = Lead.Intake("Nguyen Van A muon hoc ngay", "0901234567", "a@x.vn", LeadSource.Form, Guid.NewGuid(), "sales-a", T0);
        Assert.Equal(LeadLabel.Hot, lead.Label);
        lead.Claim(ActorRole.Sales, "sales-a", T0);
        Assert.Equal("sales-a", lead.ClaimedByActor);

        var released = lead.ReleaseIfExpired(T0.AddMinutes(5));
        Assert.True(released);
        Assert.Null(lead.ClaimedByActor);
        Assert.Null(lead.AssignedToActor);
    }

    [Fact]
    public void Claim_StaysActive_BeforeSla()
    {
        var lead = Lead.Intake("Nguyen Van A muon hoc ngay", "0901234567", "a@x.vn", LeadSource.Form, Guid.NewGuid(), "sales-a", T0);
        lead.Claim(ActorRole.Sales, "sales-a", T0);
        Assert.False(lead.ReleaseIfExpired(T0.AddMinutes(4)));
        Assert.Equal("sales-a", lead.ClaimedByActor);
    }

    [Fact]
    public void ColdOrJunk_Lead_NotAssignedToSales()
    {
        // Off hours, no contact, no campaign, call source -> Junk/Cold
        var offHour = new DateTimeOffset(2026, 8, 21, 15, 0, 0, TimeSpan.Zero);
        var lead = Lead.Intake("An", null, null, LeadSource.Call, null, "sales-a", offHour);
        Assert.True(lead.Label is LeadLabel.Cold or LeadLabel.Junk);
        Assert.Null(lead.AssignedToActor);
        Assert.Null(lead.AssignedAtUtc);
    }

    [Fact]
    public void NonSales_CannotClaim()
    {
        var lead = Lead.Intake("An", "0901234567", "a@x.vn", LeadSource.Form, null, null, T0);
        var ex = Assert.Throws<DomainRuleException>(() => lead.Claim(ActorRole.Owner, "owner-1", T0));
        Assert.Equal("ForbiddenRole", ex.Code);
    }

    [Fact]
    public void Sales_Reject_RequiresReason_AndReroutes()
    {
        var lead = Lead.Intake("Nguyen Van A can tu van", "0901234567", "a@x.vn", LeadSource.Form, Guid.NewGuid(), "sales-a", T0);
        lead.Claim(ActorRole.Sales, "sales-a", T0);

        var ex = Assert.Throws<DomainRuleException>(() => lead.Reject(ActorRole.Sales, "sales-a", "   ", "sales-b", T0));
        Assert.Equal("InvalidReason", ex.Code);

        lead.Reject(ActorRole.Sales, "sales-a", "Khach bao goi lai sau 1 tuan", "sales-b", T0);
        Assert.Null(lead.ClaimedByActor);
        Assert.Equal("sales-b", lead.AssignedToActor);
        Assert.Equal("Khach bao goi lai sau 1 tuan", lead.LastRejectionReason);
        Assert.Contains("sales-a", lead.RejectedByActors);
    }

    [Fact]
    public async Task LeadService_Reject_ReroutesToAnotherSalesActor()
    {
        var clock = new MutableClock(T0);
        var store = new InMemoryLeadStore(["sales-a", "sales-b", "sales-c"]);
        var service = new LeadService(store, clock);

        var lead = await service.IntakeFormAsync("Khach Hang can mua", "0901234567", "a@x.vn", Guid.NewGuid(), CancellationToken.None);
        Assert.Equal("sales-a", lead.AssignedToActor);

        var rejected = await service.RejectAsync(new ActorContext(ActorRole.Sales, "sales-a"), lead.Id, "Khach o xa", CancellationToken.None);
        Assert.Equal("sales-b", rejected.AssignedToActor);
        Assert.Null(rejected.ClaimedByActor);

        var rejected2 = await service.RejectAsync(new ActorContext(ActorRole.Sales, "sales-b"), lead.Id, "Khach doi y", CancellationToken.None);
        Assert.Equal("sales-c", rejected2.AssignedToActor);
    }

    private sealed class MutableClock : IClock
    {
        public MutableClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; set; }
    }

    private sealed class InMemoryLeadStore : ILeadStore
    {
        private readonly Dictionary<Guid, Lead> _leads = [];
        private readonly List<string> _sales;
        private string? _lastAssigned;

        public InMemoryLeadStore(IEnumerable<string> sales) => _sales = sales.ToList();

        public Task AddAsync(Lead lead, CancellationToken cancellationToken)
        {
            _leads[lead.Id] = lead;
            return Task.CompletedTask;
        }

        public Task<Lead?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            _leads.TryGetValue(id, out var lead);
            return Task.FromResult(lead);
        }

        public Task<Lead?> FindByPhoneOrEmailAsync(string? phone, string? email, CancellationToken cancellationToken)
        {
            var match = _leads.Values.FirstOrDefault(l =>
                (!string.IsNullOrWhiteSpace(phone) && l.Phone == phone) ||
                (!string.IsNullOrWhiteSpace(email) && l.Email == email));
            return Task.FromResult(match);
        }

        public Task<IReadOnlyList<Lead>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Lead>>(_leads.Values.ToList());
        }

        public Task UpdateAsync(Lead lead, CancellationToken cancellationToken)
        {
            _leads[lead.Id] = lead;
            return Task.CompletedTask;
        }

        public Task<int> CountAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_leads.Count);
        }

        public Task<IReadOnlyList<string>> ListSalesActorsAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<string>>(_sales);
        }

        public Task RememberSalesActorAsync(string actorId, CancellationToken cancellationToken)
        {
            if (!_sales.Contains(actorId, StringComparer.Ordinal))
            {
                _sales.Add(actorId);
            }

            return Task.CompletedTask;
        }

        public Task<string?> GetLastAssignedSalesActorAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_lastAssigned);
        }

        public Task SetLastAssignedSalesActorAsync(string actorId, CancellationToken cancellationToken)
        {
            _lastAssigned = actorId;
            return Task.CompletedTask;
        }
    }
}
