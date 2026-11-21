using FinSight.Domain.Enums;

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
