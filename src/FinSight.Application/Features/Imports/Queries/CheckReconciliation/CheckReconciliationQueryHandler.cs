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

        var closingAsOf = request.AsOf ?? batch.PeriodEnd;
        var snapshot = await ReconciliationCalculator.PreviousSnapshotAsync(
            snapshotRepository, account.Id, closingAsOf, cancellationToken);
        if (ReconciliationCalculator.StatementAccount(batch) == account.Id)
        {
            return Result.Success(ReconciliationCalculator.CheckStatement(
                account.Type, batch.StatementOpeningBalance!.Value, snapshot?.Balance.Amount,
                ReconciliationCalculator.StatementRows(batch.Rows, account.Id), request.ClosingBalance));
        }

        var rows = ReconciliationCalculator.RowsAfter(batch.Rows, account.Id, snapshot);

        return Result.Success(ReconciliationCalculator.Check(
            account.Type, snapshot?.Balance.Amount, rows, request.ClosingBalance));
    }
}
