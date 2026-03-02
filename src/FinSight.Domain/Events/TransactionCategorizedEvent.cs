using FinSight.Domain.Common;
using FinSight.Domain.Enums;

namespace FinSight.Domain.Events;

/// <summary>
/// Raised when a transaction's category changes. Handlers:
///   - Application: Recalculate budget spending totals for old and new categories
/// </summary>
public sealed record TransactionCategorizedEvent(
    Guid TransactionId,
    Guid AccountId,
    TransactionCategory OldCategory,
    TransactionCategory NewCategory) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
