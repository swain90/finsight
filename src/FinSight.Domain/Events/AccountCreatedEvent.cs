using FinSight.Domain.Common;
using FinSight.Domain.Enums;

namespace FinSight.Domain.Events;

public sealed class AccountCreatedEvent(Guid accountId, Guid userId, string name, AccountType type) : BaseEvent
{
    public Guid AccountId { get; } = accountId;
    public Guid UserId { get; } = userId;
    public string Name { get; } = name;
    public AccountType Type { get; } = type;
}
