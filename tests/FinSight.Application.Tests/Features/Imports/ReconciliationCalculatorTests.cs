using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Enums;
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
}
