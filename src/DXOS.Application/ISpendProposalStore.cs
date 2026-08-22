using DXOS.Domain;

namespace DXOS.Application;

public interface ISpendProposalStore
{
    Task AddAsync(SpendProposal proposal, CancellationToken cancellationToken);
    Task<SpendProposal?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SpendProposal>> ListAsync(CancellationToken cancellationToken);
    Task UpdateAsync(SpendProposal proposal, CancellationToken cancellationToken);
}
