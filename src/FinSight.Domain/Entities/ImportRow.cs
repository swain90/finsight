using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Entities;

public sealed class ImportRow : BaseEntity
{
    public Guid ImportBatchId { get; private set; }
    public int LineNumber { get; private set; }
    public Guid? AccountId { get; private set; }
    public DateOnly OccurredOn { get; private set; }
    public string Description { get; private set; } = string.Empty;  // max 500
    public string? MerchantName { get; private set; }  // max 200
    public Money Amount { get; private set; } = Money.Zero(Money.DefaultCurrency);
    public TransactionType Type { get; private set; }
    public TransactionCategory Category { get; private set; }
    public TransactionKind KindHint { get; private set; }
    public bool IsDuplicate { get; private set; }
    public bool IsExcluded { get; private set; }
    public bool CategoryChangedByUser { get; private set; }
    public string? Fingerprint { get; private set; }  // max 64
    public string RawLine { get; private set; } = string.Empty;  // max 1000

    private ImportRow() { }  // EF Core

    public static ImportRow Create(
        int lineNumber,
        DateOnly occurredOn,
        string description,
        string? merchantName,
        Money amount,
        TransactionType type,
        TransactionCategory category,
        TransactionKind kindHint,
        string rawLine)
    {
        var desc = (description ?? string.Empty).Trim();
        var merchant = merchantName?.Trim();
        var raw = rawLine ?? string.Empty;
        return new ImportRow
        {
            LineNumber = lineNumber,
            OccurredOn = occurredOn,
            Description = desc.Length > 500 ? desc[..500] : desc,
            MerchantName = merchant is { Length: > 200 } ? merchant[..200] : merchant,
            Amount = amount,
            Type = type,
            Category = category,
            KindHint = kindHint,
            RawLine = raw.Length > 1000 ? raw[..1000] : raw
        };
    }

    public void AssignAccount(Guid accountId, string fingerprint, bool isDuplicate)
    {
        var wasDuplicate = IsDuplicate;
        AccountId = accountId;
        Fingerprint = fingerprint;
        IsDuplicate = isDuplicate;
        // Exclusion follows duplicate transitions; otherwise the user's manual choice is kept.
        if (isDuplicate) IsExcluded = true;
        else if (wasDuplicate) IsExcluded = false;
        SetUpdatedAt();
    }

    public void SetCategory(TransactionCategory category)
    {
        Category = category;
        CategoryChangedByUser = true;
        SetUpdatedAt();
    }

    public void SetExcluded(bool excluded)
    {
        IsExcluded = excluded;
        SetUpdatedAt();
    }
}
