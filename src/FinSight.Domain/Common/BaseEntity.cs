namespace FinSight.Domain.Common;

public abstract class BaseEntity
{
    private readonly List<BaseEvent> _domainEvents = [];

    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAt { get; protected set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; protected set; }

    public IReadOnlyCollection<BaseEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(BaseEvent domainEvent) => _domainEvents.Add(domainEvent);
    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void SetUpdatedAt() => UpdatedAt = DateTimeOffset.UtcNow;
}
