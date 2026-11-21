using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Domain.Repositories;

public interface ITransactionRepository
{
    Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByUserIdPagedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByUserIdAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByUserIdAndCategoryAsync(Guid userId, TransactionCategory category, int month, int year, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetAnomaliesAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Transaction?> GetByPlaidIdAsync(string plaidTransactionId, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default);

    Task<IReadOnlySet<string>> GetExistingFingerprintsAsync(Guid accountId, IReadOnlyCollection<string> fingerprints, CancellationToken cancellationToken = default);

    /// <summary>The user's unpaired Regular/PendingTransfer transactions in range (not dismissed), with Account included.</summary>
    Task<IReadOnlyList<Transaction>> GetMatchPoolAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default);
    /// <summary>The user's PendingTransfer transactions (unpaired, not dismissed), with Account included.</summary>
    Task<IReadOnlyList<Transaction>> GetPendingTransfersAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByImportBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Transaction>> GetByTransferGroupIdsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default);
    void Remove(Transaction transaction);
}
