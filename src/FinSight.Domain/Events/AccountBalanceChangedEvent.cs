using FinSight.Domain.Common;
using FinSight.Domain.ValueObjects;

namespace FinSight.Domain.Events;

/// <summary>
/// Raised when an account's balance changes. Handlers:
///   - Infrastructure: Invalidate cached net worth calculations
///   - Infrastructure: Update dashboard real-time via SignalR (future)
/// </summary>
public sealed record AccountBalanceChangedEvent(
    Guid AccountId,
    Money NewBalance,
    string Reason) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
