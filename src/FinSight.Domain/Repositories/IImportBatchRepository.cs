using FinSight.Domain.Entities;

namespace FinSight.Domain.Repositories;

/// <summary>A batch (without its rows loaded) and its row counts, for import history.</summary>
/// <param name="Batch">The batch; <see cref="ImportBatch.Rows"/> is not loaded.</param>
/// <param name="RowCount">All parsed rows in the batch.</param>
/// <param name="IncludedRowCount">Rows not excluded: for a committed batch, the transactions it created.</param>
public sealed record ImportBatchSummary(ImportBatch Batch, int RowCount, int IncludedRowCount);

public interface IImportBatchRepository
{
    /// <summary>Returns the user's batch including its rows, or null.</summary>
    Task<ImportBatch?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The user's batches, newest first, with row counts computed in the query.</summary>
    Task<IReadOnlyList<ImportBatchSummary>> GetSummariesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(ImportBatch batch, CancellationToken cancellationToken = default);
}
