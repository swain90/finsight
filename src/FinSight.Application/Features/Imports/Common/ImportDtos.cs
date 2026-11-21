using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Application.Features.Imports.Common;

public sealed record ImportBatchDto(
    Guid Id,
    string Institution,
    string FileName,
    string Status,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    string? ReconciliationWarning,
    IReadOnlyList<ImportAccountDto> Accounts,
    IReadOnlyList<ImportRowDto> Rows);

public sealed record ImportAccountDto(
    Guid AccountId,
    string Name,
    string Type,
    bool IsLiability,
    bool IsInvestment,
    decimal? PreviousBalance,
    DateOnly? PreviousBalanceAsOf);

public sealed record ImportRowDto(
    Guid Id,
    int LineNumber,
    Guid? AccountId,
    DateOnly Date,
    string Description,
    decimal Amount,
    string Type,
    string Category,
    string KindHint,
    bool IsDuplicate,
    bool IsExcluded);

/// <param name="RowCount">All parsed rows in the batch.</param>
/// <param name="IncludedRowCount">Rows not excluded; for a committed batch, the transactions it imported.</param>
public sealed record ImportBatchSummaryDto(
    Guid Id,
    string Institution,
    string FileName,
    string Status,
    DateOnly? PeriodStart,
    DateOnly? PeriodEnd,
    int RowCount,
    int IncludedRowCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CommittedAt);

public static class ImportDtoMapper
{
    /// <param name="batch">The batch to map.</param>
    /// <param name="accounts">Accounts to expose to the review screen.</param>
    /// <param name="latestSnapshots">Latest balance snapshot per account id (missing or null = none).</param>
    public static ImportBatchDto ToDto(
        ImportBatch batch,
        IReadOnlyList<Account> accounts,
        IReadOnlyDictionary<Guid, BalanceSnapshot?> latestSnapshots) =>
        new(
            batch.Id,
            batch.Institution.ToString(),
            batch.FileName,
            batch.Status.ToString(),
            batch.PeriodStart,
            batch.PeriodEnd,
            batch.ReconciliationWarning,
            accounts.Select(a =>
            {
                var snapshot = latestSnapshots.GetValueOrDefault(a.Id);
                return new ImportAccountDto(
                    a.Id, a.Name, a.Type.ToString(), a.Type.IsLiability(), a.Type.IsInvestment(),
                    snapshot?.Balance.Amount, snapshot?.AsOf);
            }).ToList(),
            batch.Rows.OrderBy(r => r.LineNumber).Select(r => new ImportRowDto(
                r.Id, r.LineNumber, r.AccountId, r.OccurredOn, r.Description, r.Amount.Amount,
                r.Type.ToString(), r.Category.ToString(), r.KindHint.ToString(), r.IsDuplicate, r.IsExcluded)).ToList());
}
