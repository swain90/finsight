using FinSight.Domain.Common;

namespace FinSight.Domain.Events;

public sealed record AccountRenamedEvent(
    Guid AccountId,
    string OldName,
    string NewName) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
