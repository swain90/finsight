using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Events;

public sealed class TransactionRecordedEvent(
    Guid transactionId, Guid accountId, Guid userId, Money amount, TransactionType type) : BaseEvent
{
    public Guid TransactionId { get; } = transactionId;
    public Guid AccountId { get; } = accountId;
    public Guid UserId { get; } = userId;
    public Money Amount { get; } = amount;
    public TransactionType Type { get; } = type;
}
