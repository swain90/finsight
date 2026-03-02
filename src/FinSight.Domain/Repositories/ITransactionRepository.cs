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
}
