using FinSight.Domain.Entities;

namespace FinSight.Domain.Repositories;

public interface IBalanceSnapshotRepository
{
    Task<IReadOnlyList<BalanceSnapshot>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<BalanceSnapshot?> GetLatestAsync(Guid accountId, DateOnly? onOrBefore, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BalanceSnapshot>> GetByImportBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task AddAsync(BalanceSnapshot snapshot, CancellationToken cancellationToken = default);
    void Remove(BalanceSnapshot snapshot);
}
