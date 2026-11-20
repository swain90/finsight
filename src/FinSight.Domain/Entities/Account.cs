using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Events;
using FinSight.Domain.Exceptions;
using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Entities;

public sealed class Account : BaseEntity
{
    private readonly List<Transaction> _transactions = [];

    public Guid UserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public AccountType Type { get; private set; }
    public Money Balance { get; private set; } = Money.Zero(Money.DefaultCurrency);
    public string? InstitutionName { get; private set; }
    public string? PlaidAccountId { get; private set; }  // external reference
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastSyncedAt { get; private set; }

    public User User { get; private set; } = null!;
    public IReadOnlyCollection<Transaction> Transactions => _transactions.AsReadOnly();

    private Account() { }  // EF Core

    public static Account Create(Guid userId, string name, AccountType type, string currency = Money.DefaultCurrency, string? institutionName = null)
    {
        var account = new Account
        {
            UserId = userId,
            Name = name.Trim(),
            Type = type,
            Balance = Money.Zero(currency),
            InstitutionName = institutionName
        };

        account.AddDomainEvent(new AccountCreatedEvent(account.Id, userId, name, type));
        return account;
    }

    public Transaction RecordTransaction(
        string description,
        Money amount,
        TransactionType type,
        DateTimeOffset occurredAt,
        TransactionCategory category = TransactionCategory.Uncategorized,
        string? merchantName = null)
    {
        if (!IsActive)
            throw new DomainException("Cannot record transactions on an inactive account.");

        var transaction = Transaction.Create(Id, description, amount, type, occurredAt, category, merchantName);

        Balance = type == TransactionType.Credit
            ? Balance.Add(amount)
            : Balance.Subtract(amount);

        _transactions.Add(transaction);
        SetUpdatedAt();
        AddDomainEvent(new TransactionRecordedEvent(transaction.Id, Id, UserId, amount, type));

        return transaction;
    }

    public void LinkToPlaid(string plaidAccountId)
    {
        PlaidAccountId = plaidAccountId;
        LastSyncedAt = DateTimeOffset.UtcNow;
        SetUpdatedAt();
    }

    public void UpdateBalance(Money newBalance)
    {
        Balance = newBalance;
        LastSyncedAt = DateTimeOffset.UtcNow;
        SetUpdatedAt();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdatedAt();
    }
}
