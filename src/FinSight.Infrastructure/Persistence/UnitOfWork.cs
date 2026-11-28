using FinSight.Domain.Common;
using FinSight.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace FinSight.Infrastructure.Persistence;

public sealed class UnitOfWork(ApplicationDbContext context) : IUnitOfWork
{
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw new UniqueViolationException("A unique constraint was violated.", ex);
        }
    }
}
