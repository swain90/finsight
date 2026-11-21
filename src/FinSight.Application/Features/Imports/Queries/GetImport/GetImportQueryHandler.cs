using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Imports.Queries.GetImport;

public sealed class GetImportQueryHandler(
    IImportBatchRepository batchRepository,
    IAccountRepository accountRepository,
    IBalanceSnapshotRepository snapshotRepository,
    ICurrentUserService currentUserService) : IRequestHandler<GetImportQuery, Result<ImportBatchDto>>
{
    public async Task<Result<ImportBatchDto>> Handle(GetImportQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<ImportBatchDto>(Error.Unauthorized);
        var userId = currentUserService.UserId.Value;

        var batch = await batchRepository.GetByIdAsync(request.ImportId, userId, cancellationToken);
        // A discarded or undone batch no longer exists as far as the user is concerned.
        if (batch is null || batch.UserId != userId || batch.Status == ImportBatchStatus.Discarded)
            return Result.Failure<ImportBatchDto>(Error.NotFound);

        var accounts = await accountRepository.GetByUserIdAsync(userId, cancellationToken);
        return Result.Success(await ImportDtoLoader.BuildAsync(batch, accounts, snapshotRepository, cancellationToken));
    }
}
