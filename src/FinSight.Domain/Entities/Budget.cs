using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Entities;

public sealed class Budget : BaseEntity
{
    public Guid UserId { get; private set; }
    public TransactionCategory Category { get; private set; }
    public Money Limit { get; private set; } = Money.Zero(Money.DefaultCurrency);
    public Money Spent { get; private set; } = Money.Zero(Money.DefaultCurrency);
    public int Month { get; private set; }
    public int Year { get; private set; }

    public decimal SpendingPercentage => Limit.Amount == 0 ? 0 :
        Math.Min(100, (Spent.Amount / Limit.Amount) * 100);

    public Money Remaining => Limit.Subtract(Spent).Amount < 0
        ? Money.Zero(Limit.Currency)
        : Limit.Subtract(Spent);

    public bool IsOverBudget => Spent.Amount > Limit.Amount;

    private Budget() { }  // EF Core

    public static Budget Create(Guid userId, TransactionCategory category, Money limit, int month, int year)
    {
        return new Budget
        {
            UserId = userId,
            Category = category,
            Limit = limit,
            Spent = Money.Zero(limit.Currency),
            Month = month,
            Year = year
        };
    }

    public void AddSpending(Money amount)
    {
        Spent = Spent.Add(amount);
        SetUpdatedAt();
    }

    public void UpdateLimit(Money newLimit)
    {
        Limit = newLimit;
        SetUpdatedAt();
    }
}
