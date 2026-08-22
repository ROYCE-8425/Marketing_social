using DXOS.Domain;

namespace DXOS.Application;

public interface ICampaignStore
{
    Task AddAsync(Campaign campaign, CancellationToken cancellationToken);
    Task<Campaign?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<Campaign>> ListAsync(CancellationToken cancellationToken);
    Task UpdateAsync(Campaign campaign, CancellationToken cancellationToken);
}
