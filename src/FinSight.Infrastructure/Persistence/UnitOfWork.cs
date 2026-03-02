using FinSight.Domain.Common;

namespace FinSight.Infrastructure.Persistence;

public sealed class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => await context.SaveChangesAsync(cancellationToken);
}
