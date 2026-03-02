// =============================================================================
// DomainEventDispatcherInterceptor.cs — Domain Event Dispatch After Save
// =============================================================================
// Domain events collected by entities during a unit of work need to be
// dispatched AFTER the database transaction commits successfully.
//
// WHY AFTER (not before)?
// If we dispatch before save and the save fails, event handlers would have
// run (e.g., sent an email, updated a cache) but the DB has no record.
// Dispatching after ensures the DB is the source of truth.
//
// TRADE-OFF: If dispatching itself fails after save, the DB record exists
// but the event handlers didn't run. For critical events, use the
// Transactional Outbox pattern instead (persist events to a DB table,
// then publish from a background worker — Wolverine handles this natively).
// =============================================================================

using FinSight.Domain.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinSight.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Dispatches domain events through MediatR AFTER successful SaveChanges.
/// Clears the events from entities after dispatch to prevent double-publishing.
/// </summary>
public class DomainEventDispatcherInterceptor : SaveChangesInterceptor
{
    private readonly IMediator _mediator;

    public DomainEventDispatcherInterceptor(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        // Dispatch domain events only if save was successful (result > 0 or changes exist)
        await DispatchDomainEventsAsync(eventData.Context, cancellationToken);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    private async Task DispatchDomainEventsAsync(DbContext? context, CancellationToken cancellationToken)
    {
        if (context == null) return;

        // Find all tracked entities that have pending domain events
        var entities = context.ChangeTracker
            .Entries<BaseEntity>()
            .Where(e => e.Entity.DomainEvents.Any())
            .Select(e => e.Entity)
            .ToList();

        // Collect all events before dispatching (entity state might change during dispatch)
        var domainEvents = entities
            .SelectMany(e => e.DomainEvents)
            .ToList();

        // Clear events from entities BEFORE dispatching to prevent re-dispatch on retry
        entities.ForEach(e => e.ClearDomainEvents());

        // Dispatch each event through MediatR — routes to INotificationHandler<T> implementations
        foreach (var domainEvent in domainEvents)
        {
            await _mediator.Publish(domainEvent, cancellationToken);
        }
    }
}
