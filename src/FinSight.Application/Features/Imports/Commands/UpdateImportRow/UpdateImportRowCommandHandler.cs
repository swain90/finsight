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

            await AssignAccountAsync(batch, row, accountId, cancellationToken);
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

    private async Task AssignAccountAsync(ImportBatch batch, ImportRow row, Guid accountId, CancellationToken ct)
    {
        // Occurrence index among this batch's rows for the account (already assigned plus this one), in line order.
        var peers = batch.Rows
            .Where(r => r.Id == row.Id || r.AccountId == accountId)
            .OrderBy(r => r.LineNumber)
            .ToList();
        var items = peers
            .Select(r => (r.OccurredOn, Amount: Math.Round(r.Amount.Amount, 2), r.Type,
                Normalized: DescriptionNormalizer.Normalize(r.Description)))
            .ToList();
        var occurrences = ImportFingerprint.OccurrenceIndexes(items);
        var index = peers.FindIndex(r => r.Id == row.Id);
        var item = items[index];
        var fingerprint = ImportFingerprint.Compute(
            accountId, item.OccurredOn, item.Amount, item.Type, item.Normalized, occurrences[index]);

        var existing = await transactionRepository.GetExistingFingerprintsAsync(accountId, [fingerprint], ct);
        row.AssignAccount(accountId, fingerprint, existing.Contains(fingerprint));
    }
}
