// =============================================================================
// IDomainEvent.cs — Marker interface for domain events
// =============================================================================
// Domain events are facts about things that happened in the domain.
// They are named in the past tense: "TransactionCreated", "BudgetExceeded".
//
// In this architecture:
//   1. Entities collect events via AddDomainEvent()
//   2. The EF Core SaveChanges interceptor dispatches them AFTER commit
//   3. MediatR routes them to INotificationHandler<T> implementations
//
// WHY NOT JUST USE APPLICATION EVENTS?
// Domain events live in the Domain layer, keeping business rules close to the
// domain model. Application services can react to them without the domain
// knowing about application concerns (email, notifications, ML retraining).
// =============================================================================

using MediatR;

namespace FinSight.Domain.Common;

/// <summary>
/// Marker interface for all domain events.
/// Implements MediatR's INotification so events can be dispatched
/// through the existing MediatR pipeline without additional infrastructure.
/// </summary>
public interface IDomainEvent : INotification
{
    // The exact time the event occurred (UTC).
    // Critical for financial audit trails and event ordering.
    DateTimeOffset OccurredOn { get; }
}
