using DXOS.Domain;

namespace DXOS.Application;

public interface ILeadStore
{
    Task AddAsync(Lead lead, CancellationToken cancellationToken);
    Task<Lead?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Lead?> FindByPhoneOrEmailAsync(string? phone, string? email, CancellationToken cancellationToken);
    Task<IReadOnlyList<Lead>> ListAsync(CancellationToken cancellationToken);
    Task UpdateAsync(Lead lead, CancellationToken cancellationToken);
    Task<int> CountAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<string>> ListSalesActorsAsync(CancellationToken cancellationToken);
    Task RememberSalesActorAsync(string actorId, CancellationToken cancellationToken);
    Task<string?> GetLastAssignedSalesActorAsync(CancellationToken cancellationToken);
    Task SetLastAssignedSalesActorAsync(string actorId, CancellationToken cancellationToken);
}
