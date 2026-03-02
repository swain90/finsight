// =============================================================================
// IAuditableEntity.cs — Audit trail interface
// =============================================================================
// Financial applications require an immutable audit trail.
// This interface marks entities that track when they were created/modified.
//
// Implementation: The AuditableEntityInterceptor in Infrastructure
// automatically sets CreatedAt on insert and UpdatedAt on every update,
// so individual repositories never need to set these manually.
// =============================================================================

namespace FinSight.Domain.Common;

/// <summary>
/// Marks an entity as auditable — EF Core will automatically populate
/// CreatedAt and UpdatedAt via the SaveChanges interceptor.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// UTC timestamp when this record was first created.
    /// Immutable after insert — never update this field.
    /// </summary>
    DateTimeOffset CreatedAt { get; set; }

    /// <summary>
    /// UTC timestamp of the last modification.
    /// Updated automatically on every SaveChanges call.
    /// </summary>
    DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The user ID (from JWT claims) who created this record.
    /// Populated by the interceptor using ICurrentUserService.
    /// </summary>
    string? CreatedBy { get; set; }

    /// <summary>
    /// The user ID who last modified this record.
    /// </summary>
    string? UpdatedBy { get; set; }
}
