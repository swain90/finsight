using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;
using FluentAssertions;

namespace FinSight.Application.Tests.Features.Imports;

public class ReconciliationCalculatorTests
{
    [Fact]
    public void Check_AssetBalanced()
    {
        var rows = new[]
        {
            (200m, TransactionType.Credit),
            (50m, TransactionType.Debit),
        };

        var result = ReconciliationCalculator.Check(AccountType.Checking, 1000m, rows, 1150m);

        result.Skipped.Should().BeFalse();
        result.SkipReason.Should().BeNull();
        result.Expected.Should().Be(1150m);
        result.Difference.Should().Be(0m);
        result.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void Check_LiabilityBalanced()
    {
        var rows = new[]
        {
            (82.15m, TransactionType.Debit),
            (300m, TransactionType.Credit),
        };

        var result = ReconciliationCalculator.Check(AccountType.CreditCard, 300m, rows, 82.15m);

        result.Skipped.Should().BeFalse();
        result.Expected.Should().Be(82.15m);
        result.Difference.Should().Be(0m);
        result.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void Check_Mismatch_ReportsDifference()
    {
        var rows = new[]
        {
            (200m, TransactionType.Credit),
            (50m, TransactionType.Debit),
        };

        var result = ReconciliationCalculator.Check(AccountType.Checking, 1000m, rows, 1100m);

        result.Skipped.Should().BeFalse();
        result.Expected.Should().Be(1150m);
        result.Difference.Should().Be(-50m);
        result.IsBalanced.Should().BeFalse();
    }

    [Fact]
    public void Check_Tfsa_Skipped()
    {
        var rows = new[] { (200m, TransactionType.Credit) };

        var result = ReconciliationCalculator.Check(AccountType.Tfsa, 1000m, rows, 1200m);

        result.Skipped.Should().BeTrue();
        result.SkipReason.Should().Be("Investment account: market value changes with prices.");
        result.Expected.Should().BeNull();
        result.Difference.Should().BeNull();
        result.IsBalanced.Should().BeFalse();
    }

    [Fact]
    public void Check_NoPrevious_Skipped()
    {
        var rows = new[] { (200m, TransactionType.Credit) };

        var result = ReconciliationCalculator.Check(AccountType.Checking, null, rows, 200m);

        result.Skipped.Should().BeTrue();
        result.SkipReason.Should().Be("First import for this account.");
        result.Expected.Should().BeNull();
        result.Difference.Should().BeNull();
        result.IsBalanced.Should().BeFalse();
    }

    // ---- Statement equation (PDF statements: opening balance + rows = closing balance) ----

    private static readonly Guid Card = Guid.NewGuid();

    private static ImportRow StatementRow(decimal amount, TransactionType type, int day = 10,
        bool duplicate = false, bool excluded = false, Guid? account = null)
    {
        var row = ImportRow.Create(day, new DateOnly(2026, 3, day), "ROW", null, new Money(amount, "CAD"), type,
            TransactionCategory.Uncategorized, TransactionKind.Regular, "raw");
        row.AssignAccount(account ?? Card, $"fp{Guid.NewGuid():N}", duplicate);
        if (excluded) row.SetExcluded(true);
        return row;
    }

    private static ImportBatch StatementBatch(decimal? opening, params ImportRow[] rows)
    {
        var batch = ImportBatch.Create(Guid.NewGuid(), Institution.Cibc, "statement.pdf");
        foreach (var r in rows) batch.AddRow(r);
        if (opening is { } o) batch.ApplyStatementSummary(new DateOnly(2026, 3, 1), new DateOnly(2026, 3, 31), o, 0m);
        return batch;
    }

    [Fact]
    public void StatementAccount_PdfBatchWithOneAccount_ReturnsIt()
    {
        var batch = StatementBatch(100m, StatementRow(10m, TransactionType.Debit), StatementRow(5m, TransactionType.Debit));

        ReconciliationCalculator.StatementAccount(batch).Should().Be(Card);
    }

    [Fact]
    public void StatementAccount_NoOpeningBalance_ReturnsNull()
    {
        var batch = StatementBatch(null, StatementRow(10m, TransactionType.Debit));

        ReconciliationCalculator.StatementAccount(batch).Should().BeNull();
    }

    [Fact]
    public void StatementAccount_RowsSplitAcrossAccounts_ReturnsNull()
    {
        var batch = StatementBatch(100m,
            StatementRow(10m, TransactionType.Debit), StatementRow(5m, TransactionType.Debit, account: Guid.NewGuid()));

        ReconciliationCalculator.StatementAccount(batch).Should().BeNull();
    }

    [Fact]
    public void CheckStatement_Balanced_NoWarning()
    {
        var batch = StatementBatch(100m,
            StatementRow(40m, TransactionType.Debit), StatementRow(25m, TransactionType.Credit));
        var rows = ReconciliationCalculator.StatementRows(batch.Rows, Card);

        // Liability: 100 owed + 40 charged - 25 paid = 115 owed.
        var result = ReconciliationCalculator.CheckStatement(AccountType.CreditCard, 100m, 100m, rows, 115m);

        result.Skipped.Should().BeFalse();
        result.Expected.Should().Be(115m);
        result.Difference.Should().Be(0m);
        result.IsBalanced.Should().BeTrue();
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void CheckStatement_RowDatedBeforePreviousSnapshot_StillCounts()
    {
        // Charges dated just before the period start belong to this statement (CIBC posts by posting date).
        var batch = StatementBatch(100m, StatementRow(30m, TransactionType.Debit, day: 1));
        var rows = ReconciliationCalculator.StatementRows(batch.Rows, Card);

        var result = ReconciliationCalculator.CheckStatement(AccountType.CreditCard, 100m, 100m, rows, 130m);

        result.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void CheckStatement_ManuallyExcludedRow_NotCounted()
    {
        var batch = StatementBatch(100m,
            StatementRow(40m, TransactionType.Debit), StatementRow(15m, TransactionType.Debit, excluded: true));
        var rows = ReconciliationCalculator.StatementRows(batch.Rows, Card);

        var result = ReconciliationCalculator.CheckStatement(AccountType.CreditCard, 100m, null, rows, 155m);

        result.Expected.Should().Be(140m);
        result.Difference.Should().Be(15m);
        result.IsBalanced.Should().BeFalse();
        result.Warning.Should().BeNull();
    }

    [Fact]
    public void CheckStatement_DuplicateExcludedRow_StillCounts()
    {
        var duplicate = StatementRow(15m, TransactionType.Debit, duplicate: true);
        duplicate.IsExcluded.Should().BeTrue();
        var batch = StatementBatch(100m, StatementRow(40m, TransactionType.Debit), duplicate);
        var rows = ReconciliationCalculator.StatementRows(batch.Rows, Card);

        var result = ReconciliationCalculator.CheckStatement(AccountType.CreditCard, 100m, null, rows, 155m);

        result.Expected.Should().Be(155m);
        result.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void CheckStatement_AssetSign_CreditsAdd()
    {
        var batch = StatementBatch(100m,
            StatementRow(40m, TransactionType.Debit), StatementRow(25m, TransactionType.Credit));
        var rows = ReconciliationCalculator.StatementRows(batch.Rows, Card);

        var result = ReconciliationCalculator.CheckStatement(AccountType.Checking, 100m, null, rows, 85m);

        result.Expected.Should().Be(85m);
        result.IsBalanced.Should().BeTrue();
    }

    [Fact]
    public void CheckStatement_PreviousBalanceDiffersFromOpening_WarnsWithBothAmounts()
    {
        var batch = StatementBatch(100m, StatementRow(40m, TransactionType.Debit));
        var rows = ReconciliationCalculator.StatementRows(batch.Rows, Card);

        var result = ReconciliationCalculator.CheckStatement(AccountType.CreditCard, 100m, 75m, rows, 140m);

        result.Skipped.Should().BeFalse();
        result.Difference.Should().Be(0m);
        result.IsBalanced.Should().BeFalse();
        result.Warning.Should().Be(
            "Balance on record $75.00 differs from the statement's opening balance $100.00. " +
            "A statement in between may be missing.");
    }

    [Fact]
    public void CheckStatement_Investment_Skipped()
    {
        var result = ReconciliationCalculator.CheckStatement(AccountType.Tfsa, 100m, 50m, [], 140m);

        result.Skipped.Should().BeTrue();
        result.Warning.Should().BeNull();
    }
}
