using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Imports.Commands.UpdateImportRow;

public sealed class UpdateImportRowCommandHandler(
    IImportBatchRepository batchRepository,
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateImportRowCommand, Result<ImportRowDto>>
{
    public async Task<Result<ImportRowDto>> Handle(UpdateImportRowCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<ImportRowDto>(Error.Unauthorized);
        var userId = currentUserService.UserId.Value;

        var batch = await batchRepository.GetByIdAsync(request.ImportId, userId, cancellationToken);
        if (batch is null || batch.UserId != userId)
            return Result.Failure<ImportRowDto>(Error.NotFound);

        var row = batch.Rows.FirstOrDefault(r => r.Id == request.RowId);
        if (row is null)
            return Result.Failure<ImportRowDto>(Error.NotFound);

        if (batch.Status != ImportBatchStatus.PendingReview)
            return Result.Failure<ImportRowDto>(Error.Custom("Import.NotPending",
                "This import has already been committed or discarded."));

        if (request.AccountId is { } accountId)
        {
            var account = (await accountRepository.GetByUserIdAsync(userId, cancellationToken))
                .FirstOrDefault(a => a.Id == accountId);
            if (account is null)
                return Result.Failure<ImportRowDto>(Error.NotFound);
            if (!account.IsActive)
                return Result.Failure<ImportRowDto>(Error.Custom("Import.AccountInactive",
                    "That account is inactive. Reactivate it or pick another account."));

            var previousAccountId = row.AccountId;
            await ImportRowFingerprinter.RecomputeAccountAsync(
                batch.Rows, accountId, row, transactionRepository, cancellationToken);
            if (previousAccountId is { } oldId && oldId != accountId)
                await ImportRowFingerprinter.RecomputeAccountAsync(
                    batch.Rows, oldId, null, transactionRepository, cancellationToken);
        }

        if (request.Category is { } category)
            row.SetCategory(category);
        if (request.IsExcluded is { } excluded)
            row.SetExcluded(excluded);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new ImportRowDto(
            row.Id, row.LineNumber, row.AccountId, row.OccurredOn, row.Description, row.Amount.Amount,
            row.Type.ToString(), row.Category.ToString(), row.KindHint.ToString(), row.IsDuplicate, row.IsExcluded));
    }
}
