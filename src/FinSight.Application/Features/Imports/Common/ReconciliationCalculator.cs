using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;

namespace FinSight.Application.Features.Imports.Common;

/// <summary>Outcome of checking a statement's closing balance against the previous balance plus its rows.</summary>
public sealed record ReconciliationResult(
    bool Skipped,
    string? SkipReason,
    decimal? Expected,
    decimal? Difference,
    bool IsBalanced);

/// <summary>Verifies that previous balance + imported rows equals the statement's closing balance.</summary>
public static class ReconciliationCalculator
{
    private const decimal Tolerance = 0.005m;

    /// <summary>
    /// The snapshot a statement closing on <paramref name="closingAsOf"/> is reconciled against: the latest one
    /// dated strictly before the closing date (R25), so backfilling an older month never compares against a
    /// newer statement. A null date means "the latest snapshot".
    /// </summary>
    public static Task<BalanceSnapshot?> PreviousSnapshotAsync(
        IBalanceSnapshotRepository snapshots, Guid accountId, DateOnly? closingAsOf, CancellationToken cancellationToken) =>
        snapshots.GetLatestAsync(accountId, closingAsOf?.AddDays(-1), cancellationToken);

    /// <summary>
    /// The account's included (not excluded) rows dated after <paramref name="previous"/>; rows on or before it
    /// are already reflected in that snapshot (overlapping statements).
    /// </summary>
    public static IEnumerable<(decimal Amount, TransactionType Type)> RowsAfter(
        IEnumerable<ImportRow> rows, Guid accountId, BalanceSnapshot? previous) =>
        rows.Where(r => r.AccountId == accountId && !r.IsExcluded && (previous is null || r.OccurredOn > previous.AsOf))
            .Select(r => (r.Amount.Amount, r.Type));

    /// <summary>Checks the closing balance of a statement against the expected balance.</summary>
    /// <param name="type">Account type; liabilities invert the sign convention.</param>
    /// <param name="previousBalance">Balance from the prior import, or null when there is none.</param>
    /// <param name="rows">Parsed rows as (amount, direction) pairs.</param>
    /// <param name="closingBalance">Closing balance reported by the statement.</param>
    /// <returns>A reconciliation result; skipped when reconciliation is not meaningful.</returns>
    public static ReconciliationResult Check(
        AccountType type,
        decimal? previousBalance,
        IEnumerable<(decimal Amount, TransactionType Type)> rows,
        decimal closingBalance)
    {
        if (type.IsInvestment())
        {
            return new ReconciliationResult(
                Skipped: true,
                SkipReason: "Investment account: market value changes with prices.",
                Expected: null,
                Difference: null,
                IsBalanced: false);
        }

        if (previousBalance is null)
        {
            return new ReconciliationResult(
                Skipped: true,
                SkipReason: "First import for this account.",
                Expected: null,
                Difference: null,
                IsBalanced: false);
        }

        decimal credits = 0m;
        decimal debits = 0m;
        foreach (var (amount, transactionType) in rows)
        {
            if (transactionType == TransactionType.Credit)
            {
                credits += amount;
            }
            else
            {
                debits += amount;
            }
        }

        var expected = type.IsLiability()
            ? previousBalance.Value + debits - credits
            : previousBalance.Value + credits - debits;

        var difference = closingBalance - expected;

        return new ReconciliationResult(
            Skipped: false,
            SkipReason: null,
            Expected: expected,
            Difference: difference,
            IsBalanced: Math.Abs(difference) < Tolerance);
    }
}
