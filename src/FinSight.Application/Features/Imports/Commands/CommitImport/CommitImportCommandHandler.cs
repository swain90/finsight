using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using MediatR;

namespace FinSight.Application.Features.Imports.Commands.CommitImport;

public sealed class CommitImportCommandHandler(
    IImportBatchRepository batchRepository,
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IBalanceSnapshotRepository snapshotRepository,
    IMerchantRuleRepository merchantRuleRepository,
    ITransferMatchingService matchingService,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<CommitImportCommand, Result<ImportBatchDto>>
{
    public async Task<Result<ImportBatchDto>> Handle(CommitImportCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<ImportBatchDto>(Error.Unauthorized);
        var userId = currentUserService.UserId.Value;

        var batch = await batchRepository.GetByIdAsync(request.ImportId, userId, cancellationToken);
        if (batch is null || batch.UserId != userId)
            return Result.Failure<ImportBatchDto>(Error.NotFound);
        if (batch.Status != ImportBatchStatus.PendingReview)
            return Result.Failure<ImportBatchDto>(Error.Custom("Import.NotPending",
                "This import has already been committed or discarded."));

        var included = batch.Rows.Where(r => !r.IsExcluded).ToList();
        if (included.Any(r => r.AccountId is null))
            return Result.Failure<ImportBatchDto>(Error.Custom("Import.UnassignedRows",
                "Assign an account to every row before committing."));

        var userAccounts = await accountRepository.GetByUserIdAsync(userId, cancellationToken);
        var accountsById = userAccounts.ToDictionary(a => a.Id);

        var includedByAccount = included.GroupBy(r => r.AccountId!.Value).ToList();
        foreach (var group in includedByAccount)
        {
            if (!accountsById.TryGetValue(group.Key, out var account))
                return Result.Failure<ImportBatchDto>(Error.NotFound);
            if (!account.IsActive)
                return Result.Failure<ImportBatchDto>(Error.Custom("Import.AccountInactive",
                    "That account is inactive. Reactivate it or pick another account."));
            if (request.Balances.All(b => b.AccountId != group.Key))
                return Result.Failure<ImportBatchDto>(Error.Custom("Import.MissingBalance",
                    $"Enter a closing balance for {account.Name}."));
        }

        if (request.Balances.Any(b => !accountsById.ContainsKey(b.AccountId)))
            return Result.Failure<ImportBatchDto>(Error.NotFound);

        // Reconcile against the latest snapshot as it is BEFORE this import's snapshots are recorded.
        var warnings = new List<string>();
        foreach (var input in request.Balances)
        {
            var account = accountsById[input.AccountId];
            var previous = await snapshotRepository.GetLatestAsync(account.Id, null, cancellationToken);
            var rows = included
                .Where(r => r.AccountId == account.Id)
                .Select(r => (r.Amount.Amount, r.Type));
            var check = ReconciliationCalculator.Check(account.Type, previous?.Balance.Amount, rows, input.ClosingBalance);
            if (!check.Skipped && !check.IsBalanced)
                warnings.Add($"{account.Name}: off by {check.Difference:C}");
        }

        var transactions = new List<Transaction>(included.Count);
        try
        {
            foreach (var row in included)
            {
                var account = accountsById[row.AccountId!.Value];
                transactions.Add(account.ImportTransaction(
                    row.Description,
                    new Money(row.Amount.Amount, row.Amount.Currency),
                    row.Type,
                    new DateTimeOffset(row.OccurredOn.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
                    row.Category,
                    row.KindHint,
                    batch.Id,
                    row.Fingerprint!,
                    row.MerchantName));
            }
        }
        catch (DomainException ex)
        {
            return Result.Failure<ImportBatchDto>(Error.Custom("Import.Invalid", ex.Message));
        }

        var snapshots = new List<BalanceSnapshot>(request.Balances.Count);
        foreach (var input in request.Balances)
        {
            var account = accountsById[input.AccountId];
            snapshots.Add(account.RecordBalanceSnapshot(
                input.AsOf, new Money(input.ClosingBalance, account.Balance.Currency), batch.Id));
        }

        // Learn merchant categories: one rule per normalized description, newest user choice wins.
        var rulesByKey = (await merchantRuleRepository.GetByUserIdAsync(userId, cancellationToken))
            .GroupBy(r => r.DescriptionKey)
            .ToDictionary(g => g.Key, g => g.First());
        var newRules = new List<MerchantRule>();
        foreach (var row in batch.Rows.Where(r => r.CategoryChangedByUser))
        {
            var key = DescriptionNormalizer.Normalize(row.Description);
            if (key.Length == 0)
                continue;
            if (rulesByKey.TryGetValue(key, out var rule))
            {
                rule.Update(row.Category);
            }
            else
            {
                rule = MerchantRule.Create(userId, key, row.Category);
                rulesByKey[key] = rule;
                newRules.Add(rule);
            }
        }

        batch.Commit(warnings.Count == 0 ? null : string.Join("; ", warnings));

        await transactionRepository.AddRangeAsync(transactions, cancellationToken);
        foreach (var snapshot in snapshots)
            await snapshotRepository.AddAsync(snapshot, cancellationToken);
        foreach (var rule in newRules)
            await merchantRuleRepository.AddAsync(rule, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (batch.PeriodStart is { } from && batch.PeriodEnd is { } to)
        {
            await matchingService.RunAsync(userId, from, to, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(await ImportDtoLoader.BuildAsync(batch, userAccounts, snapshotRepository, cancellationToken));
    }
}
