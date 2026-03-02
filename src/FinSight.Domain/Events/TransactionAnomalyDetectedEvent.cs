using FinSight.Domain.Common;
using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Events;

/// <summary>
/// Raised when ML detects an unusual transaction. Handlers:
///   - Infrastructure: Send push notification to user
///   - Infrastructure: Log to anomaly audit table
/// </summary>
public sealed record TransactionAnomalyDetectedEvent(
    Guid TransactionId,
    Guid AccountId,
    Money Amount,
    string Reason) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
