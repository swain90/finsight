using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Domain.Repositories;

public interface IBudgetRepository
{
    Task<Budget?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Budget>> GetByUserIdAsync(Guid userId, int month, int year, CancellationToken cancellationToken = default);
    Task<Budget?> GetByCategoryAsync(Guid userId, TransactionCategory category, int month, int year, CancellationToken cancellationToken = default);
    Task AddAsync(Budget budget, CancellationToken cancellationToken = default);
    Task UpdateAsync(Budget budget, CancellationToken cancellationToken = default);
}
