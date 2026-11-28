using FinSight.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<Account> Accounts { get; }
    DbSet<Transaction> Transactions { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<BalanceSnapshot> BalanceSnapshots { get; }
    DbSet<ImportBatch> ImportBatches { get; }
    DbSet<ImportRow> ImportRows { get; }
    DbSet<MerchantRule> MerchantRules { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
