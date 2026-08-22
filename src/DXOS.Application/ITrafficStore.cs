using DXOS.Domain;

namespace DXOS.Application;

public interface ITrafficStore
{
    Task AddSnapshotAsync(TrafficSnapshot snapshot, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrafficSnapshot>> ListByCampaignAsync(Guid campaignId, CancellationToken cancellationToken);
    Task<IReadOnlyList<TrafficSnapshot>> ListAllAsync(CancellationToken cancellationToken);
    Task<decimal> GetTotalSpendVndAsync(CancellationToken cancellationToken);
    Task<decimal> GetCampaignTotalSpendVndAsync(Guid campaignId, CancellationToken cancellationToken);
}
