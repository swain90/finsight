using FinSight.Domain.Common;
using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Entities;

public sealed class BalanceSnapshot : BaseEntity
{
    public Guid AccountId { get; private set; }
    public DateOnly AsOf { get; private set; }
    public Money Balance { get; private set; } = Money.Zero(Money.DefaultCurrency);
    public Guid? ImportBatchId { get; private set; }

    private BalanceSnapshot() { }  // EF Core

    public static BalanceSnapshot Create(Guid accountId, DateOnly asOf, Money balance, Guid? importBatchId) =>
        new() { AccountId = accountId, AsOf = asOf, Balance = balance, ImportBatchId = importBatchId };
}
