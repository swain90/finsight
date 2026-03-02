// =============================================================================
// AuditableEntityInterceptor.cs — Auto-populate audit fields
// =============================================================================
// This SaveChangesInterceptor automatically sets CreatedAt, UpdatedAt,
// CreatedBy, and UpdatedBy fields on any entity implementing IAuditableEntity.
//
// Without this interceptor, every repository method would need to manually
// set these fields — error-prone and repetitive. Centralizing it here ensures
// no entity ever accidentally has a missing audit timestamp.
//
// HOW IT WORKS:
// EF Core calls SavingChangesAsync BEFORE writing to the database.
// We iterate all tracked entities that are Added or Modified,
// check if they implement IAuditableEntity, and update the fields.
// =============================================================================

using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace FinSight.Infrastructure.Persistence.Interceptors;

/// <summary>
/// EF Core interceptor that automatically sets CreatedAt, UpdatedAt, CreatedBy, UpdatedBy
/// on every save operation. Never needs to be called manually.
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService _currentUser;

    public AuditableEntityInterceptor(ICurrentUserService currentUser)
    {
        _currentUser = currentUser;
    }

    /// <summary>
    /// Called synchronously before SaveChanges. We override the async version
    /// but EF Core may call this in sync contexts, so both must be overridden.
    /// </summary>
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateAuditFields(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateAuditFields(DbContext? context)
    {
        if (context == null) return;

        var now = DateTimeOffset.UtcNow;
        var currentUser = _currentUser.UserId?.ToString();

        // Iterate all entities that EF Core is tracking as Added or Modified
        foreach (var entry in context.ChangeTracker.Entries<IAuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    // Set both timestamps on insert — CreatedAt is immutable after this
                    entry.Entity.CreatedAt = now;
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.CreatedBy = currentUser;
                    entry.Entity.UpdatedBy = currentUser;
                    break;

                case EntityState.Modified:
                    // Only update the "updated" fields — never change CreatedAt on update
                    entry.Entity.UpdatedAt = now;
                    entry.Entity.UpdatedBy = currentUser;

                    // Mark CreatedAt and CreatedBy as unmodified to prevent accidental overwrite
                    entry.Property(nameof(IAuditableEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(IAuditableEntity.CreatedBy)).IsModified = false;
                    break;
            }
        }
    }
}
