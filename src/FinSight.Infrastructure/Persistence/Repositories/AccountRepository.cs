using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class AccountRepository(ApplicationDbContext context) : IAccountRepository
{
    public async Task<Account?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Accounts.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<Account?> GetByIdWithTransactionsAsync(Guid id, CancellationToken cancellationToken = default)
        => await context.Accounts
            .Include(a => a.Transactions)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Account>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await context.Accounts
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Name)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Account account, CancellationToken cancellationToken = default)
        => await context.Accounts.AddAsync(account, cancellationToken);

    public Task UpdateAsync(Account account, CancellationToken cancellationToken = default)
    {
        var entry = context.Entry(account);

        // Avoid graph-wide state changes (Account + Transactions) that can
        // incorrectly mark newly added child entities as Modified.
        if (entry.State == EntityState.Detached)
        {
            context.Attach(account);
            entry = context.Entry(account);
        }

        if (entry.State == EntityState.Unchanged)
            entry.State = EntityState.Modified;

        return Task.CompletedTask;
    }

    public async Task<bool> BelongsToUserAsync(Guid accountId, Guid userId, CancellationToken cancellationToken = default)
        => await context.Accounts.AnyAsync(a => a.Id == accountId && a.UserId == userId, cancellationToken);
}
