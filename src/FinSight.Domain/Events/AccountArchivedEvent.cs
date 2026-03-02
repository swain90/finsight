using FinSight.Domain.Common;

namespace FinSight.Domain.Events;

public sealed record AccountArchivedEvent(Guid AccountId) : IDomainEvent
{
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
