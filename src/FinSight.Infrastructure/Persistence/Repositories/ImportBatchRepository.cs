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

    public async Task<IReadOnlyList<ImportBatchSummary>> GetSummariesByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var rows = await context.ImportBatches
            .AsNoTracking()
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new
            {
                Batch = b,
                RowCount = context.ImportRows.Count(r => r.ImportBatchId == b.Id),
                IncludedRowCount = context.ImportRows.Count(r => r.ImportBatchId == b.Id && !r.IsExcluded),
            })
            .ToListAsync(cancellationToken);
        return rows.Select(x => new ImportBatchSummary(x.Batch, x.RowCount, x.IncludedRowCount)).ToList();
    }

    public async Task AddAsync(ImportBatch batch, CancellationToken cancellationToken = default)
        => await context.ImportBatches.AddAsync(batch, cancellationToken);
}
