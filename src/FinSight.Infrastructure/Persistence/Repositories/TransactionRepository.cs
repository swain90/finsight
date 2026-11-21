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
            .Include(t => t.Account)
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

    public async Task<IReadOnlySet<string>> GetExistingFingerprintsAsync(Guid accountId, IReadOnlyCollection<string> fingerprints, CancellationToken cancellationToken = default)
    {
        var found = await context.Transactions
            .Where(t => t.AccountId == accountId && t.ImportFingerprint != null && fingerprints.Contains(t.ImportFingerprint))
            .Select(t => t.ImportFingerprint!)
            .Distinct()
            .ToListAsync(cancellationToken);
        return found.ToHashSet();
    }

    public async Task<IReadOnlyList<Transaction>> GetMatchPoolAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken = default)
    {
        var fromUtc = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toUtc = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        return await context.Transactions
            .Include(t => t.Account)
            .Where(t => t.Account.UserId == userId
                && t.OccurredAt >= fromUtc && t.OccurredAt < toUtc
                && (t.Kind == TransactionKind.Regular || t.Kind == TransactionKind.PendingTransfer)
                && t.TransferGroupId == null
                && !t.IsTransferDismissed)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Transaction>> GetByImportBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
        => await context.Transactions.Where(t => t.ImportBatchId == batchId).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Transaction>> GetByTransferGroupIdsAsync(IReadOnlyCollection<Guid> groupIds, CancellationToken cancellationToken = default)
        => await context.Transactions
            .Where(t => t.TransferGroupId != null && groupIds.Contains(t.TransferGroupId.Value))
            .ToListAsync(cancellationToken);

    public void Remove(Transaction transaction) => context.Transactions.Remove(transaction);
}
