using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Events;

/// <summary>
/// Raised when a new Transaction is created. Handlers:
///   - Application: Trigger ML categorization if category is Uncategorized
///   - Application: Run anomaly detection against spending patterns
///   - Application: Update budget spending totals
///   - Infrastructure: Update account balance
/// </summary>
public sealed record TransactionCreatedEvent(
    Guid TransactionId,
    Guid AccountId,
    Money Amount,
    TransactionType Type,
    string MerchantName,
    TransactionCategory Category) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
