using DXOS.Application;
using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class PlatformLeadIntakeTests
{
    private static readonly DateTimeOffset OffHourUtc = new(2026, 8, 21, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FacebookLeadAds_PhoneEmailCampaignOffHours_Scores87Hot()
    {
        var (score, label, breakdown, reasons) = LeadScoring.Calculate(
            "Nguyen Van A",
            "0901234567",
            "a@example.com",
            LeadSource.Facebook,
            Guid.NewGuid(),
            OffHourUtc);

        Assert.Equal(87, score);
        Assert.Equal(LeadLabel.Hot, label);
        Assert.Equal(40, breakdown.Behavior);
        Assert.Equal(17, breakdown.Channel);
        Assert.Equal(20, breakdown.Campaign);
        Assert.Equal(5, breakdown.Time);
        Assert.Equal(5, breakdown.Intent);
        Assert.Equal(LeadScoring.ModelId, "rules");
        Assert.Contains(reasons, r => r.Contains("Facebook", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Webhook_SameExternalEventId_IsIdempotent()
    {
        var clock = new FixedClock(OffHourUtc);
        var store = new MemoryLeadStore(["kinh-doanh-an"]);
        var webhooks = new MemoryWebhookStore();
        var service = new LeadService(store, clock, webhooks);

        var first = await service.IntakePlatformWebhookAsync(
            "facebook",
            "fb_evt_001",
            "Nguyen Van A",
            "0901234567",
            "a@example.com",
            Guid.NewGuid(),
            null,
            CancellationToken.None);

        var second = await service.IntakePlatformWebhookAsync(
            "facebook",
            "fb_evt_001",
            "Nguyen Van A",
            "0901234567",
            "a@example.com",
            Guid.NewGuid(),
            null,
            CancellationToken.None);

        Assert.False(first.Duplicate);
        Assert.True(second.Duplicate);
        Assert.Equal(first.Lead.Id, second.Lead.Id);
        Assert.Single(store.Leads);
        Assert.Equal(87, first.Lead.Score);
        Assert.Equal(LeadLabel.Hot, first.Lead.Label);
        Assert.Equal("rules", first.Lead.ScoreModel);
        Assert.False(string.IsNullOrWhiteSpace(first.Lead.AssignedToActor));
    }

    [Fact]
    public async Task Webhook_SamePhoneAcrossFacebookAndZalo_MergesIdentities()
    {
        var clock = new FixedClock(OffHourUtc);
        var store = new MemoryLeadStore(["kinh-doanh-an"]);
        var webhooks = new MemoryWebhookStore();
        var service = new LeadService(store, clock, webhooks);

        var facebook = await service.IntakePlatformWebhookAsync(
            "facebook", "fb_1", "Nguyen Van A", "0901234567", "a@example.com", null, null, CancellationToken.None);
        var zalo = await service.IntakePlatformWebhookAsync(
            "zalo", "zalo_1", "Nguyen Van A", "0901234567", "a@example.com", null, null, CancellationToken.None);

        Assert.Single(store.Leads);
        Assert.Equal(facebook.Lead.Id, zalo.Lead.Id);
        Assert.Contains(LeadSource.Facebook, zalo.Lead.Sources);
        Assert.Contains(LeadSource.Zalo, zalo.Lead.Sources);

        var summary = await service.SummarizeByPlatformAsync(CancellationToken.None);
        Assert.Contains(summary, row => row.Provider == "facebook" && row.LeadCount == 1);
        Assert.Contains(summary, row => row.Provider == "zalo" && row.LeadCount == 1);
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTimeOffset utcNow) => UtcNow = utcNow;
        public DateTimeOffset UtcNow { get; }
    }

    private sealed class MemoryWebhookStore : IWebhookEventStore
    {
        private readonly Dictionary<string, (Guid Id, Guid? LeadId)> _events = new(StringComparer.Ordinal);

        public Task<WebhookBeginResult> TryBeginAsync(string provider, string externalEventId, string payloadHash, CancellationToken cancellationToken)
        {
            var key = provider + "|" + externalEventId;
            if (_events.TryGetValue(key, out var existing))
            {
                return Task.FromResult(new WebhookBeginResult(true, existing.Id, existing.LeadId));
            }

            var id = Guid.NewGuid();
            _events[key] = (id, null);
            return Task.FromResult(new WebhookBeginResult(false, id, null));
        }

        public Task CompleteAsync(Guid webhookEventId, Guid leadId, CancellationToken cancellationToken)
        {
            foreach (var pair in _events.ToList())
            {
                if (pair.Value.Id == webhookEventId)
                {
                    _events[pair.Key] = (webhookEventId, leadId);
                }
            }

            return Task.CompletedTask;
        }
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
