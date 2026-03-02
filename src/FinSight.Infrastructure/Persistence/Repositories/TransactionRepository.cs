using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class TransactionRepository(ApplicationDbContext context) : ITransactionRepository
{
    public async Task<Transaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Transactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByUserIdPagedAsync(Guid userId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await context.Transactions
            .Where(t => t.Account.UserId == userId)
            .OrderByDescending(t => t.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByAccountIdAsync(Guid accountId, int page, int pageSize, CancellationToken cancellationToken = default)
        => await context.Transactions
            .Where(t => t.AccountId == accountId)
            .OrderByDescending(t => t.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByUserIdAsync(Guid userId, DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken = default)
        => await context.Transactions
            .Where(t => t.Account.UserId == userId && t.OccurredAt >= from && t.OccurredAt <= to)
            .OrderByDescending(t => t.OccurredAt)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByUserIdAndCategoryAsync(Guid userId, TransactionCategory category, int month, int year, CancellationToken cancellationToken = default)
        => await context.Transactions
            .Where(t => t.Account.UserId == userId
                && t.Category == category
                && t.OccurredAt.Month == month
                && t.OccurredAt.Year == year)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetAnomaliesAsync(Guid userId, CancellationToken cancellationToken = default)
        => await context.Transactions
            .Where(t => t.Account.UserId == userId && t.IsAnomaly)
            .OrderByDescending(t => t.OccurredAt)
            .ToListAsync(cancellationToken);

    public async Task<Transaction?> GetByPlaidIdAsync(string plaidTransactionId, CancellationToken cancellationToken = default)
        => await context.Transactions
            .FirstOrDefaultAsync(t => t.PlaidTransactionId == plaidTransactionId, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Transaction> transactions, CancellationToken cancellationToken = default)
        => await context.Transactions.AddRangeAsync(transactions, cancellationToken);
}
