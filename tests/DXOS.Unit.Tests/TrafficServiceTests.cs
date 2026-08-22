using DXOS.Application;
using DXOS.Domain;
using Xunit;

namespace DXOS.Unit.Tests;

public sealed class TrafficServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 8, 21);

    private sealed class InMemoryTrafficStore : ITrafficStore
    {
        private readonly List<TrafficSnapshot> _snapshots = [];

        public Task AddSnapshotAsync(TrafficSnapshot snapshot, CancellationToken cancellationToken)
        {
            _snapshots.Add(snapshot);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<TrafficSnapshot>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken)
        {
            var list = _snapshots.Where(s => s.CampaignId == campaignId).ToList();
            return Task.FromResult<IReadOnlyList<TrafficSnapshot>>(list);
        }

        public Task<IReadOnlyList<TrafficSnapshot>> ListAllAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<TrafficSnapshot>>(_snapshots.ToList());
        }

        public Task<decimal> GetTotalSpendVndAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(_snapshots.Sum(s => s.SpendVnd));
        }

        public Task<decimal> GetCampaignTotalSpendVndAsync(Guid campaignId, CancellationToken cancellationToken)
        {
            return Task.FromResult(_snapshots.Where(s => s.CampaignId == campaignId).Sum(s => s.SpendVnd));
        }
    }

    private sealed class InMemoryCampaignStore : ICampaignStore
    {
        public readonly Dictionary<Guid, Campaign> Campaigns = [];

        public Task AddAsync(Campaign campaign, CancellationToken cancellationToken)
        {
            Campaigns[campaign.Id] = campaign;
            return Task.CompletedTask;
        }

        public Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken)
        {
            Campaigns.TryGetValue(id, out var campaign);
            return Task.FromResult(campaign);
        }

        public Task<IReadOnlyList<Campaign>> ListAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<Campaign>>(Campaigns.Values.ToList());
        }

        public Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken)
        {
            Campaigns[campaign.Id] = campaign;
            return Task.CompletedTask;
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    [Theory]
    [InlineData(ActorRole.Marketer)]
    [InlineData(ActorRole.Owner)]
    [InlineData(ActorRole.System)]
    public async Task AllowedRoles_CanRecordTraffic(ActorRole role)
    {
        var campaignStore = new InMemoryCampaignStore();
        var trafficStore = new InMemoryTrafficStore();
        var service = new TrafficService(trafficStore, campaignStore, new FixedClock());

        var campaign = Campaign.CreateDraft("Summer Promo", "copy", "mai", Now);
        await campaignStore.AddAsync(campaign, TestContext.Current.CancellationToken);

        var actor = new ActorContext(role, "actor-1");
        var result = await service.RecordSnapshotAsync(actor, campaign.Id, Today, 1000, 50, 40, 500_000, TestContext.Current.CancellationToken);

        Assert.NotNull(result.Snapshot);
        Assert.Equal(1000, result.Totals.Impressions);
        Assert.Equal(50, result.Totals.Clicks);
        Assert.Equal(500_000m, result.Totals.SpendVnd);
    }

    [Theory]
    [InlineData(ActorRole.Sales)]
    [InlineData(ActorRole.Content)]
    public async Task DisallowedRoles_ThrowForbiddenRole(ActorRole role)
    {
        var campaignStore = new InMemoryCampaignStore();
        var trafficStore = new InMemoryTrafficStore();
        var service = new TrafficService(trafficStore, campaignStore, new FixedClock());

        var campaign = Campaign.CreateDraft("Summer Promo", "copy", "mai", Now);
        await campaignStore.AddAsync(campaign, TestContext.Current.CancellationToken);

        var actor = new ActorContext(role, "sales-1");
        var ex = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.RecordSnapshotAsync(actor, campaign.Id, Today, 1000, 50, 40, 500_000, TestContext.Current.CancellationToken));

        Assert.Equal("ForbiddenRole", ex.Code);
    }

    [Fact]
    public async Task NonExistentCampaign_ThrowsNotFound()
    {
        var campaignStore = new InMemoryCampaignStore();
        var trafficStore = new InMemoryTrafficStore();
        var service = new TrafficService(trafficStore, campaignStore, new FixedClock());

        var actor = new ActorContext(ActorRole.Marketer, "mai");
        var ex = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.RecordSnapshotAsync(actor, Guid.NewGuid(), Today, 1000, 50, 40, 500_000, TestContext.Current.CancellationToken));

        Assert.Equal("NotFound", ex.Code);
    }

    [Fact]
    public async Task RejectedCampaign_ThrowsInvalidTransition()
    {
        var campaignStore = new InMemoryCampaignStore();
        var trafficStore = new InMemoryTrafficStore();
        var service = new TrafficService(trafficStore, campaignStore, new FixedClock());

        var campaign = Campaign.CreateDraft("Summer Promo", "copy", "mai", Now);
        campaign.SubmitReview(ActorRole.Marketer, Now);
        campaign.Reject(ActorRole.Marketer, Now);
        await campaignStore.AddAsync(campaign, TestContext.Current.CancellationToken);

        var actor = new ActorContext(ActorRole.Marketer, "mai");
        var ex = await Assert.ThrowsAsync<DomainRuleException>(() =>
            service.RecordSnapshotAsync(actor, campaign.Id, Today, 1000, 50, 40, 500_000, TestContext.Current.CancellationToken));

        Assert.Equal("InvalidTransition", ex.Code);
    }
}
