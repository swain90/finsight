using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Events;

/// <summary>
/// Raised the first time a budget's spending crosses the limit.
/// Only raised once per budget period — not on every subsequent transaction.
/// Handlers: Send budget alert notification to user.
/// </summary>
public sealed record BudgetExceededEvent(
    Guid BudgetId,
    string UserId,
    TransactionCategory Category,
    Money Limit,
    Money ActualSpent) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
