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

        var parsedResult = parser.Parse(request.Content);
        if (parsedResult.IsFailure)
            return Result.Failure<ImportBatchDto>(parsedResult.Error);
        var parsed = parsedResult.Value;

        if (request.AccountId is { } requestedId &&
            !await accountRepository.BelongsToUserAsync(requestedId, userId, cancellationToken))
            return Result.Failure<ImportBatchDto>(Error.NotFound);

        var userAccounts = await accountRepository.GetByUserIdAsync(userId, cancellationToken);
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

        // Fingerprints, per account, with occurrence indexes over that account's rows in file order.
        var fingerprints = new string?[resolved.Count];
        foreach (var group in resolved.Select((r, i) => (r, i)).Where(x => x.r.AccountId is not null)
                     .GroupBy(x => x.r.AccountId!.Value))
        {
            var items = group.ToList();
            var occurrences = ImportFingerprint.OccurrenceIndexes(
                items.Select(x => (x.r.Row.Date, x.r.Amount, x.r.Row.Type, x.r.Normalized)).ToList());
            for (var k = 0; k < items.Count; k++)
            {
                var x = items[k];
                fingerprints[x.i] = ImportFingerprint.Compute(
                    group.Key, x.r.Row.Date, x.r.Amount, x.r.Row.Type, x.r.Normalized, occurrences[k]);
            }
        }

        var existing = new Dictionary<Guid, IReadOnlySet<string>>();
        foreach (var group in resolved.Select((r, i) => (r, i)).Where(x => fingerprints[x.i] is not null)
                     .GroupBy(x => x.r.AccountId!.Value))
        {
            var prints = group.Select(x => fingerprints[x.i]!).ToList();
            existing[group.Key] = await transactionRepository.GetExistingFingerprintsAsync(group.Key, prints, cancellationToken);
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
                importRow.AssignAccount(id, fp, existing[id].Contains(fp));

            batch.AddRow(importRow);
        }

        await batchRepository.AddAsync(batch, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var assignedIds = batch.Rows.Where(r => r.AccountId is not null).Select(r => r.AccountId!.Value).ToHashSet();
        var shown = userAccounts
            .Where(a => assignedIds.Contains(a.Id) || institutionAccounts.Any(c => c.Id == a.Id))
            .ToList();
        var snapshots = new Dictionary<Guid, BalanceSnapshot?>();
        foreach (var account in shown)
            snapshots[account.Id] = await snapshotRepository.GetLatestAsync(account.Id, null, cancellationToken);

        return Result.Success(ImportDtoMapper.ToDto(batch, shown, snapshots));
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
