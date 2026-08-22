using DXOS.Domain;

namespace DXOS.Application;

public sealed class TrafficService
{
    private readonly ITrafficStore _trafficStore;
    private readonly ICampaignStore _campaignStore;
    private readonly IClock _clock;

    public TrafficService(ITrafficStore trafficStore, ICampaignStore campaignStore, IClock clock)
    {
        _trafficStore = trafficStore;
        _campaignStore = campaignStore;
        _clock = clock;
    }

    public async Task<TrafficIngestResult> RecordSnapshotAsync(
        ActorContext actor,
        Guid campaignId,
        DateOnly periodDate,
        long impressions,
        long clicks,
        long visits,
        decimal spendVnd,
        CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        if (actor.Role is not (ActorRole.Marketer or ActorRole.Owner or ActorRole.System))
        {
            throw new DomainRuleException("ForbiddenRole", "Only Marketer, Owner, or System can record traffic.");
        }

        var campaign = await _campaignStore.GetAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            throw new DomainRuleException("NotFound", $"Campaign '{campaignId}' was not found.");
        }

        if (campaign.Status == CampaignStatus.Rejected)
        {
            throw new DomainRuleException("InvalidTransition", "Cannot record traffic for a rejected campaign.");
        }

        var snapshot = TrafficSnapshot.Create(
            campaignId,
            periodDate,
            impressions,
            clicks,
            visits,
            spendVnd,
            actor.ActorId,
            _clock.UtcNow);

        await _trafficStore.AddSnapshotAsync(snapshot, cancellationToken);

        var allSnapshots = await _trafficStore.ListByCampaignAsync(campaignId, cancellationToken);
        var totals = CampaignTrafficTotals.Aggregate(allSnapshots);

        return new TrafficIngestResult(snapshot, totals);
    }

    public async Task<CampaignTrafficSummary> GetCampaignTrafficAsync(
        Guid campaignId,
        CancellationToken cancellationToken)
    {
        var campaign = await _campaignStore.GetAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            throw new DomainRuleException("NotFound", $"Campaign '{campaignId}' was not found.");
        }

        var snapshots = await _trafficStore.ListByCampaignAsync(campaignId, cancellationToken);
        var totals = CampaignTrafficTotals.Aggregate(snapshots);

        return new CampaignTrafficSummary(campaign, snapshots, totals);
    }

    public Task<decimal> GetTotalStoredSpendVndAsync(CancellationToken cancellationToken)
    {
        return _trafficStore.GetTotalSpendVndAsync(cancellationToken);
    }

    private static void EnsureActor(ActorContext actor)
    {
        if (string.IsNullOrWhiteSpace(actor.ActorId))
        {
            throw new DomainRuleException("InvalidActor", "X-DXOS-Actor is required.");
        }
    }
}

public sealed record TrafficIngestResult(TrafficSnapshot Snapshot, CampaignTrafficTotals Totals);
public sealed record CampaignTrafficSummary(Campaign Campaign, IReadOnlyList<TrafficSnapshot> Snapshots, CampaignTrafficTotals Totals);
