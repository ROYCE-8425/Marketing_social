using DXOS.Domain;

namespace DXOS.Application;

public sealed class CampaignService
{
    private readonly ICampaignStore _store;
    private readonly CampaignCopyStub _copyStub;
    private readonly IClock _clock;

    public CampaignService(ICampaignStore store, CampaignCopyStub copyStub, IClock clock)
    {
        _store = store;
        _copyStub = copyStub;
        _clock = clock;
    }

    public async Task<Campaign> CreateDraftAsync(ActorContext actor, string topic, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        if (actor.Role == ActorRole.Sales)
        {
            throw new DomainRuleException("ForbiddenRole", "Sales cannot create campaigns.");
        }

        var copy = _copyStub.DraftFromTopic(topic);
        var campaign = Campaign.CreateDraft(topic, copy, actor.ActorId, _clock.UtcNow);
        await _store.AddAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> SubmitReviewAsync(ActorContext actor, Guid campaignId, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        campaign.SubmitReview(actor.Role, _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> SendToOwnerAsync(ActorContext actor, Guid campaignId, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        campaign.SendToOwner(actor.Role, _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> ApproveAsync(ActorContext actor, Guid campaignId, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        campaign.Approve(actor.Role, _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> UndoApprovalAsync(ActorContext actor, Guid campaignId, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        campaign.UndoApproval(actor.Role, _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public async Task<Campaign> RejectAsync(ActorContext actor, Guid campaignId, string? reason, CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var campaign = await GetRequiredAsync(campaignId, cancellationToken);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainRuleException("InvalidReason", "Lý do từ chối chiến dịch là bắt buộc.");
        }

        campaign.Reject(actor.Role, reason, _clock.UtcNow);
        await _store.UpdateAsync(campaign, cancellationToken);
        return campaign;
    }

    public Task<Campaign?> GetAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        return _store.GetAsync(campaignId, cancellationToken);
    }

    public Task<IReadOnlyList<Campaign>> ListAsync(CancellationToken cancellationToken)
    {
        return _store.ListAsync(cancellationToken);
    }

    private async Task<Campaign> GetRequiredAsync(Guid campaignId, CancellationToken cancellationToken)
    {
        var campaign = await _store.GetAsync(campaignId, cancellationToken);
        if (campaign is null)
        {
            throw new DomainRuleException("NotFound", $"Campaign '{campaignId}' was not found.");
        }

        return campaign;
    }

    private static void EnsureActor(ActorContext actor)
    {
        if (string.IsNullOrWhiteSpace(actor.ActorId))
        {
            throw new DomainRuleException("InvalidActor", "X-DXOS-Actor is required.");
        }
    }
}
