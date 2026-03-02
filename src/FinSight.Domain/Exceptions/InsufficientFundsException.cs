using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Exceptions;

/// <summary>
/// Thrown when a debit would cause a non-credit account to go negative.
/// Provides specific details for meaningful error messages.
/// HTTP mapping: 422 Unprocessable Entity.
/// </summary>
public class InsufficientFundsException : DomainException
{
    public InsufficientFundsException(Guid accountId, Money balance, Money requested)
        : base($"Insufficient funds in account {accountId}. " +
               $"Balance: {balance}, Requested debit: {requested}.")
    {
        AccountId = accountId;
        CurrentBalance = balance;
        RequestedAmount = requested;
    }

    public Guid AccountId { get; }
    public Money CurrentBalance { get; }
    public Money RequestedAmount { get; }
}
