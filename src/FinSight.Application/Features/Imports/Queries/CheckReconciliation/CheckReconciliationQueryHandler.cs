using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Imports.Queries.CheckReconciliation;

public sealed class CheckReconciliationQueryHandler(
    IImportBatchRepository batchRepository,
    IAccountRepository accountRepository,
    IBalanceSnapshotRepository snapshotRepository,
    ICurrentUserService currentUserService) : IRequestHandler<CheckReconciliationQuery, Result<ReconciliationResult>>
{
    public async Task<Result<ReconciliationResult>> Handle(
        CheckReconciliationQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<ReconciliationResult>(Error.Unauthorized);
        var userId = currentUserService.UserId.Value;

        var batch = await batchRepository.GetByIdAsync(request.ImportId, userId, cancellationToken);
        if (batch is null || batch.UserId != userId)
            return Result.Failure<ReconciliationResult>(Error.NotFound);

        var account = (await accountRepository.GetByUserIdAsync(userId, cancellationToken))
            .FirstOrDefault(a => a.Id == request.AccountId);
        if (account is null)
            return Result.Failure<ReconciliationResult>(Error.NotFound);

        var snapshot = await snapshotRepository.GetLatestAsync(account.Id, null, cancellationToken);
        var rows = batch.Rows
            .Where(r => r.AccountId == account.Id && !r.IsExcluded)
            .Select(r => (r.Amount.Amount, r.Type));

        return Result.Success(ReconciliationCalculator.Check(
            account.Type, snapshot?.Balance.Amount, rows, request.ClosingBalance));
    }
}
