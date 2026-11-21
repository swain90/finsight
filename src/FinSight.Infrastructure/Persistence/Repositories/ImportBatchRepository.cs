using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace FinSight.Infrastructure.Persistence.Repositories;

public sealed class ImportBatchRepository(ApplicationDbContext context) : IImportBatchRepository
{
    public async Task<ImportBatch?> GetByIdAsync(Guid id, Guid userId, CancellationToken cancellationToken = default)
        => await context.ImportBatches
            .Include(b => b.Rows)
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<ImportBatch>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await context.ImportBatches
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(ImportBatch batch, CancellationToken cancellationToken = default)
        => await context.ImportBatches.AddAsync(batch, cancellationToken);
}
