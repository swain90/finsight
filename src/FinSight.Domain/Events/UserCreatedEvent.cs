using FinSight.Domain.Common;

namespace FinSight.Domain.Events;

public sealed class UserCreatedEvent(Guid userId, string email) : BaseEvent
{
    public Guid UserId { get; } = userId;
    public string Email { get; } = email;
}
