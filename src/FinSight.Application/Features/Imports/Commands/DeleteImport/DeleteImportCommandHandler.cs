using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Imports.Commands.DeleteImport;

public sealed class DeleteImportCommandHandler(
    IImportBatchRepository batchRepository,
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IBalanceSnapshotRepository snapshotRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteImportCommand, Result>
{
    public async Task<Result> Handle(DeleteImportCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure(Error.Unauthorized);
        var userId = currentUserService.UserId.Value;

        var batch = await batchRepository.GetByIdAsync(request.ImportId, userId, cancellationToken);
        if (batch is null || batch.UserId != userId)
            return Result.Failure(Error.NotFound);

        switch (batch.Status)
        {
            case ImportBatchStatus.Discarded:
                return Result.Failure(Error.Custom("Import.AlreadyDiscarded", "This import was already removed."));
            case ImportBatchStatus.PendingReview:
                batch.Discard();
                break;
            default:
                await UndoAsync(batch, userId, cancellationToken);
                break;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task UndoAsync(ImportBatch batch, Guid userId, CancellationToken cancellationToken)
    {
        var batchTransactions = await transactionRepository.GetByImportBatchIdAsync(batch.Id, cancellationToken);
        var batchTxIds = batchTransactions.Select(t => t.Id).ToHashSet();

        // Partners of paired rows that live outside this batch go back to waiting for a match.
        var groupIds = batchTransactions
            .Where(t => t.TransferGroupId.HasValue)
            .Select(t => t.TransferGroupId!.Value)
            .Distinct()
            .ToList();
        if (groupIds.Count > 0)
        {
            var linked = await transactionRepository.GetByTransferGroupIdsAsync(groupIds, cancellationToken);
            foreach (var partner in linked.Where(t => !batchTxIds.Contains(t.Id)))
            {
                // Matching gave the credit leg of a corporation-to-personal pair the income category
                // PayFromCorporation; it must not stay income once the pair is gone.
                var wasOwnerPay = partner.Kind == TransactionKind.OwnerPay;
                partner.Unpair();
                if (wasOwnerPay && partner.Category == TransactionCategory.PayFromCorporation)
                    partner.Categorize(TransactionCategory.Uncategorized);
            }
        }

        var batchSnapshots = await snapshotRepository.GetByImportBatchIdAsync(batch.Id, cancellationToken);
        var removedSnapshotIds = batchSnapshots.Select(s => s.Id).ToHashSet();

        foreach (var tx in batchTransactions)
            transactionRepository.Remove(tx);
        foreach (var snapshot in batchSnapshots)
            snapshotRepository.Remove(snapshot);

        // Removals are not saved yet, so work out "latest remaining" in memory to keep undo atomic.
        var affectedAccountIds = batchTransactions.Select(t => t.AccountId)
            .Concat(batchSnapshots.Select(s => s.AccountId))
            .ToHashSet();
        var remaining = (await snapshotRepository.GetByUserIdAsync(userId, cancellationToken))
            .Where(s => !removedSnapshotIds.Contains(s.Id) && affectedAccountIds.Contains(s.AccountId))
            .GroupBy(s => s.AccountId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(s => s.AsOf).ThenByDescending(s => s.CreatedAt).First());

        foreach (var account in (await accountRepository.GetByUserIdAsync(userId, cancellationToken))
                     .Where(a => affectedAccountIds.Contains(a.Id)))
            account.ResetBalanceFromSnapshot(remaining.GetValueOrDefault(account.Id));

        batch.MarkUndone();
    }
}
