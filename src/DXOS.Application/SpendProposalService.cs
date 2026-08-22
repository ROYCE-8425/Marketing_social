using DXOS.Domain;

namespace DXOS.Application;

public sealed class SpendProposalService
{
    private readonly ISpendProposalStore _store;
    private readonly IClock _clock;

    public SpendProposalService(ISpendProposalStore store, IClock clock)
    {
        _store = store;
        _clock = clock;
    }

    public async Task<SpendProposal> ProposeAsync(
        ActorContext actor,
        string fromNote,
        string toNote,
        decimal percent,
        string rationale,
        CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var proposal = SpendProposal.Propose(actor.Role, actor.ActorId, fromNote, toNote, percent, rationale, _clock.UtcNow);
        await _store.AddAsync(proposal, cancellationToken);
        return proposal;
    }

    public async Task<SpendProposal> ApproveAsync(
        ActorContext actor,
        Guid proposalId,
        CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var proposal = await GetRequiredAsync(proposalId, cancellationToken);
        proposal.Approve(actor.Role, actor.ActorId, _clock.UtcNow);
        await _store.UpdateAsync(proposal, cancellationToken);
        return proposal;
    }

    public async Task<SpendProposal> RejectAsync(
        ActorContext actor,
        Guid proposalId,
        string? reason,
        CancellationToken cancellationToken)
    {
        EnsureActor(actor);
        var proposal = await GetRequiredAsync(proposalId, cancellationToken);
        proposal.Reject(actor.Role, actor.ActorId, reason, _clock.UtcNow);
        await _store.UpdateAsync(proposal, cancellationToken);
        return proposal;
    }

    public Task<IReadOnlyList<SpendProposal>> ListAsync(CancellationToken cancellationToken)
    {
        return _store.ListAsync(cancellationToken);
    }

    public Task<SpendProposal?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        return _store.GetAsync(id, cancellationToken);
    }

    private async Task<SpendProposal> GetRequiredAsync(Guid id, CancellationToken cancellationToken)
    {
        var proposal = await _store.GetAsync(id, cancellationToken);
        if (proposal is null)
        {
            throw new DomainRuleException("NotFound", $"Spend proposal '{id}' was not found.");
        }

        return proposal;
    }

    private static void EnsureActor(ActorContext actor)
    {
        if (string.IsNullOrWhiteSpace(actor.ActorId))
        {
            throw new DomainRuleException("InvalidActor", "X-DXOS-Actor is required.");
        }
    }
}
