using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Imports.Queries.GetImports;

public sealed class GetImportsQueryHandler(
    IImportBatchRepository batchRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetImportsQuery, Result<IReadOnlyList<ImportBatchSummaryDto>>>
{
    public async Task<Result<IReadOnlyList<ImportBatchSummaryDto>>> Handle(
        GetImportsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<IReadOnlyList<ImportBatchSummaryDto>>(Error.Unauthorized);

        var batches = await batchRepository.GetSummariesByUserIdAsync(currentUserService.UserId.Value, cancellationToken);
        IReadOnlyList<ImportBatchSummaryDto> summaries = batches
            .OrderByDescending(s => s.Batch.CreatedAt)
            .Select(s => new ImportBatchSummaryDto(
                s.Batch.Id, s.Batch.Institution.ToString(), s.Batch.FileName, s.Batch.Status.ToString(),
                s.Batch.PeriodStart, s.Batch.PeriodEnd, s.RowCount, s.IncludedRowCount,
                s.Batch.CreatedAt, s.Batch.CommittedAt))
            .ToList();
        return Result.Success(summaries);
    }
}
