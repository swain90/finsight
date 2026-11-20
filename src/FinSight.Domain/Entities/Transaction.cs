using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Entities;

public sealed class Transaction : BaseEntity
{
    public Guid AccountId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public Money Amount { get; private set; } = Money.Zero(Money.DefaultCurrency);
    public TransactionType Type { get; private set; }
    public TransactionCategory Category { get; private set; }
    public string? MerchantName { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }
    public bool IsAnomaly { get; private set; }
    public string? Notes { get; private set; }
    public string? PlaidTransactionId { get; private set; }

    public Account Account { get; private set; } = null!;

    private Transaction() { }  // EF Core

    internal static Transaction Create(
        Guid accountId,
        string description,
        Money amount,
        TransactionType type,
        DateTimeOffset occurredAt,
        TransactionCategory category,
        string? merchantName)
    {
        return new Transaction
        {
            AccountId = accountId,
            Description = description.Trim(),
            Amount = amount,
            Type = type,
            OccurredAt = occurredAt,
            Category = category,
            MerchantName = merchantName?.Trim()
        };
    }

    public void Categorize(TransactionCategory category)
    {
        Category = category;
        SetUpdatedAt();
    }

    public void MarkAsAnomaly()
    {
        IsAnomaly = true;
        SetUpdatedAt();
    }

    public void AddNotes(string notes)
    {
        Notes = notes.Trim();
        SetUpdatedAt();
    }

    public void LinkToPlaid(string plaidTransactionId)
    {
        PlaidTransactionId = plaidTransactionId;
        SetUpdatedAt();
    }
}
