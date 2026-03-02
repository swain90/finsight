using FinSight.Domain.Entities;

namespace FinSight.Domain.Repositories;

public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Account?> GetByIdWithTransactionsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Account>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task AddAsync(Account account, CancellationToken cancellationToken = default);
    Task UpdateAsync(Account account, CancellationToken cancellationToken = default);
    Task<bool> BelongsToUserAsync(Guid accountId, Guid userId, CancellationToken cancellationToken = default);
}
