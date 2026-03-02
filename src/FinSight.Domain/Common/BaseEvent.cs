namespace FinSight.Domain.Common;

public abstract class BaseEvent : IDomainEvent
{
    public Guid Id { get; } = Guid.NewGuid();
    public DateTimeOffset OccurredOn { get; } = DateTimeOffset.UtcNow;
}
