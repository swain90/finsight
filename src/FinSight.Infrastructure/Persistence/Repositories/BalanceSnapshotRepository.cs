using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class BalanceSnapshotRepository(ApplicationDbContext context) : IBalanceSnapshotRepository
{
    public async Task<IReadOnlyList<BalanceSnapshot>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await context.BalanceSnapshots
            .Where(s => context.Accounts.Any(a => a.Id == s.AccountId && a.UserId == userId))
            .OrderBy(s => s.AsOf)
            .ToListAsync(cancellationToken);

    public async Task<BalanceSnapshot?> GetLatestAsync(Guid accountId, DateOnly? onOrBefore, CancellationToken cancellationToken = default)
        => await context.BalanceSnapshots
            .Where(s => s.AccountId == accountId && (onOrBefore == null || s.AsOf <= onOrBefore))
            .OrderByDescending(s => s.AsOf)
            .ThenByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<BalanceSnapshot>> GetByImportBatchIdAsync(Guid batchId, CancellationToken cancellationToken = default)
        => await context.BalanceSnapshots
            .Where(s => s.ImportBatchId == batchId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(BalanceSnapshot snapshot, CancellationToken cancellationToken = default)
        => await context.BalanceSnapshots.AddAsync(snapshot, cancellationToken);

    public void Remove(BalanceSnapshot snapshot) => context.BalanceSnapshots.Remove(snapshot);
}
