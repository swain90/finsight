using FinSight.Domain.Entities;

namespace FinSight.Domain.Repositories;

public interface IImportBatchRepository
{
    /// <summary>Returns the user's batch including its rows, or null.</summary>
    Task<ImportBatch?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ImportBatch>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(ImportBatch batch, CancellationToken cancellationToken = default);
}
