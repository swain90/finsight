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

        var batches = await batchRepository.GetByUserIdAsync(currentUserService.UserId.Value, cancellationToken);
        IReadOnlyList<ImportBatchSummaryDto> summaries = batches
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new ImportBatchSummaryDto(
                b.Id, b.Institution.ToString(), b.FileName, b.Status.ToString(),
                b.PeriodStart, b.PeriodEnd, b.Rows.Count, b.CreatedAt, b.CommittedAt))
            .ToList();
        return Result.Success(summaries);
    }
}
