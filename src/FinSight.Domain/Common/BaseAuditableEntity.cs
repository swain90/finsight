namespace FinSight.Domain.Common;

public abstract class BaseAuditableEntity : BaseEntity
{
    public Guid CreatedByUserId { get; protected set; }
    public Guid? UpdatedByUserId { get; protected set; }
}
