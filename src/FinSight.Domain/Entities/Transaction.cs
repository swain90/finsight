using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
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
    public TransactionKind Kind { get; private set; } = TransactionKind.Regular;
    public Guid? TransferGroupId { get; private set; }
    public Guid? ImportBatchId { get; private set; }
    public string? ImportFingerprint { get; private set; }  // max 64
    public bool IsTransferDismissed { get; private set; }

    public Account Account { get; private set; } = null!;

    private Transaction() { }  // EF Core

    internal static Transaction Create(
        Guid accountId,
        string description,
        Money amount,
        TransactionType type,
        DateTimeOffset occurredAt,
        TransactionCategory category,
        string? merchantName,
        Account? account = null,
        TransactionKind kind = TransactionKind.Regular,
        Guid? importBatchId = null,
        string? fingerprint = null)
    {
        return new Transaction
        {
            AccountId = accountId,
            Description = description.Trim(),
            Amount = amount,
            Type = type,
            OccurredAt = occurredAt,
            Category = category,
            MerchantName = merchantName?.Trim(),
            Kind = kind,
            ImportBatchId = importBatchId,
            ImportFingerprint = fingerprint,
            Account = account!
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

    public void MarkPaired(Guid transferGroupId, TransactionKind kind, TransactionCategory? category = null)
    {
        if (kind is not (TransactionKind.Transfer or TransactionKind.OwnerPay))
            throw new DomainException("A paired transaction must be a Transfer or OwnerPay.");

        TransferGroupId = transferGroupId;
        Kind = kind;
        if (category.HasValue) Category = category.Value;
        SetUpdatedAt();
    }

    public void MarkPendingTransfer()
    {
        Kind = TransactionKind.PendingTransfer;
        SetUpdatedAt();
    }

    public void MarkNotTransfer()
    {
        Kind = TransactionKind.Regular;
        TransferGroupId = null;
        IsTransferDismissed = true;
        SetUpdatedAt();
    }

    public void Unpair()
    {
        Kind = TransactionKind.PendingTransfer;
        TransferGroupId = null;
        SetUpdatedAt();
    }
}
