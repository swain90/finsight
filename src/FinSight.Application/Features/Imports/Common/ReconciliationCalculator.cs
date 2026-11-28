using System.Globalization;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;

namespace FinSight.Application.Features.Imports.Common;

/// <summary>Outcome of checking a statement's closing balance against the previous balance plus its rows.</summary>
/// <param name="Warning">
/// A problem other than the closing difference, e.g. the balance on record not matching the statement's opening
/// balance; null when there is none.
/// </param>
public sealed record ReconciliationResult(
    bool Skipped,
    string? SkipReason,
    decimal? Expected,
    decimal? Difference,
    bool IsBalanced,
    string? Warning = null);

/// <summary>Verifies that previous balance + imported rows equals the statement's closing balance.</summary>
public static class ReconciliationCalculator
{
    /// <summary>Differences smaller than half a cent count as balanced.</summary>
    public const decimal Tolerance = 0.005m;

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

    /// <summary>
    /// The account a statement batch reconciles by the statement's own equation: set only when the batch carries a
    /// statement opening balance (PDF) and all of its assigned rows belong to exactly one account. Otherwise null,
    /// and the batch reconciles against the previous snapshot as before.
    /// </summary>
    public static Guid? StatementAccount(ImportBatch batch)
    {
        if (batch.StatementOpeningBalance is null)
            return null;
        var accounts = batch.Rows.Where(r => r.AccountId is not null).Select(r => r.AccountId!.Value).Distinct().ToList();
        return accounts.Count == 1 ? accounts[0] : null;
    }

    /// <summary>
    /// The account's rows that belong to the statement: every row except those the user excluded. A row excluded
    /// only because it was already imported (a duplicate) is still on the statement, so it counts. Rows are not
    /// filtered by date: the statement assigns rows by posting date, and some carry a transaction date before
    /// the period start.
    /// </summary>
    public static IEnumerable<(decimal Amount, TransactionType Type)> StatementRows(
        IEnumerable<ImportRow> rows, Guid accountId) =>
        rows.Where(r => r.AccountId == accountId && (!r.IsExcluded || r.IsDuplicate))
            .Select(r => (r.Amount.Amount, r.Type));

    /// <summary>
    /// Checks a statement against itself (opening balance + rows = closing balance), then checks continuity:
    /// the balance on record before this statement must equal the statement's opening balance.
    /// </summary>
    /// <param name="type">Account type; liabilities invert the sign convention.</param>
    /// <param name="openingBalance">Opening balance printed on the statement.</param>
    /// <param name="previousBalance">Balance of the previous snapshot, or null when there is none.</param>
    /// <param name="rows">The statement's rows, from <see cref="StatementRows"/>.</param>
    /// <param name="closingBalance">The closing balance the user entered.</param>
    public static ReconciliationResult CheckStatement(
        AccountType type,
        decimal openingBalance,
        decimal? previousBalance,
        IEnumerable<(decimal Amount, TransactionType Type)> rows,
        decimal closingBalance)
    {
        var equation = Check(type, openingBalance, rows, closingBalance);
        if (equation.Skipped)
            return equation;

        string? warning = null;
        if (previousBalance is { } previous && Math.Abs(previous - openingBalance) >= Tolerance)
        {
            var money = CultureInfo.GetCultureInfo("en-CA");
            warning = $"Balance on record {previous.ToString("C", money)} differs from the statement's opening " +
                      $"balance {openingBalance.ToString("C", money)}. A statement in between may be missing.";
        }

        return equation with { IsBalanced = equation.IsBalanced && warning is null, Warning = warning };
    }

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
