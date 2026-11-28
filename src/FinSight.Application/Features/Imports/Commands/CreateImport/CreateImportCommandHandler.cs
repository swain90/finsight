using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using MediatR;

namespace FinSight.Application.Features.Imports.Commands.CreateImport;

public sealed class CreateImportCommandHandler(
    IStatementParser parser,
    IAccountRepository accountRepository,
    IImportBatchRepository batchRepository,
    IMerchantRuleRepository merchantRuleRepository,
    IBalanceSnapshotRepository snapshotRepository,
    ITransactionRepository transactionRepository,
    ITransactionCategorizationService categorizer,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateImportCommand, Result<ImportBatchDto>>
{
    public async Task<Result<ImportBatchDto>> Handle(CreateImportCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<ImportBatchDto>(Error.Unauthorized);
        var userId = currentUserService.UserId.Value;

        var parsedResult = parser.Parse(request.Content, request.FileName);
        if (parsedResult.IsFailure)
            return Result.Failure<ImportBatchDto>(parsedResult.Error);
        var parsed = parsedResult.Value;

        if (request.AccountId is { } requestedId &&
            !await accountRepository.BelongsToUserAsync(requestedId, userId, cancellationToken))
            return Result.Failure<ImportBatchDto>(Error.NotFound);

        var userAccounts = await accountRepository.GetByUserIdAsync(userId, cancellationToken);
        if (request.AccountId is { } explicitId)
        {
            var explicitAccount = userAccounts.FirstOrDefault(a => a.Id == explicitId);
            if (explicitAccount is null)
                return Result.Failure<ImportBatchDto>(Error.NotFound);
            if (!explicitAccount.IsActive)
                return Result.Failure<ImportBatchDto>(Error.Custom("Import.AccountInactive",
                    "That account is inactive. Reactivate it or pick another account."));
        }

        var institutionAccounts = userAccounts
            .Where(a => a.IsActive && a.Institution == parsed.Institution)
            .ToList();
        var rules = (await merchantRuleRepository.GetByUserIdAsync(userId, cancellationToken))
            .GroupBy(r => r.DescriptionKey)
            .ToDictionary(g => g.Key, g => g.First().Category);

        // Resolve rows (normalized description, amount, assigned account).
        var resolved = parsed.Rows.Select(row =>
        {
            var normalized = DescriptionNormalizer.Normalize(row.Description);
            var accountId = request.AccountId ?? PickAccount(institutionAccounts, row.ExternalAccountRef);
            return (Row: row, Normalized: normalized, Amount: Math.Round(row.Amount, 2), AccountId: accountId);
        }).ToList();

        // Fingerprints and duplicate flags, per account, over that account's rows in file order.
        var fingerprints = new (string Fingerprint, bool IsDuplicate)?[resolved.Count];
        foreach (var group in resolved.Select((r, i) => (r, i)).Where(x => x.r.AccountId is not null)
                     .GroupBy(x => x.r.AccountId!.Value))
        {
            var items = group.ToList();
            var results = await ImportRowFingerprinter.ComputeAsync(
                group.Key,
                items.Select(x => (x.r.Row.Date, x.r.Amount, x.r.Row.Type, x.r.Normalized)).ToList(),
                transactionRepository, cancellationToken);
            for (var k = 0; k < items.Count; k++)
                fingerprints[items[k].i] = results[k];
        }

        var batch = ImportBatch.Create(userId, parsed.Institution, request.FileName);
        for (var i = 0; i < resolved.Count; i++)
        {
            var (row, normalized, amount, accountId) = resolved[i];

            var category = rules.TryGetValue(normalized, out var ruleCategory)
                ? ruleCategory
                : row.CategoryHint ?? categorizer.Categorize(row.MerchantName ?? string.Empty, row.Description);

            var kind = row.KindHint == TransactionKind.Regular && TransferMatcher.HasHint(normalized)
                ? TransactionKind.PendingTransfer
                : row.KindHint;

            var importRow = ImportRow.Create(
                row.LineNumber, row.Date, row.Description, row.MerchantName,
                new Money(amount, row.Currency), row.Type, category, kind, row.RawLine);

            if (accountId is { } id && fingerprints[i] is { } fp)
                importRow.AssignAccount(id, fp.Fingerprint, fp.IsDuplicate);

            batch.AddRow(importRow);
        }

        if (parsed.Summary is { } summary)
            batch.ApplyStatementSummary(
                summary.PeriodStart, summary.PeriodEnd, summary.OpeningBalance, summary.ClosingBalance);

        await batchRepository.AddAsync(batch, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await ImportDtoLoader.BuildAsync(batch, userAccounts, snapshotRepository, cancellationToken));
    }

    private static Guid? PickAccount(IReadOnlyList<Account> candidates, string? externalRef)
    {
        var matches = string.IsNullOrWhiteSpace(externalRef)
            ? candidates
            : candidates.Where(a => !string.IsNullOrWhiteSpace(a.ExternalAccountRef) &&
                                    externalRef.EndsWith(a.ExternalAccountRef, StringComparison.Ordinal)).ToList();
        return matches.Count == 1 ? matches[0].Id : null;
    }
}
