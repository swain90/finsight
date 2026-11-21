using FinSight.Application.Common.Reporting;
using FluentAssertions;
using Xunit;

namespace FinSight.Application.Tests.Common;

public sealed class FiscalYearTests
{
    [Fact]
    public void Range_Dec31_2026()
    {
        FiscalYear.Range(12, 31, 2026).Should().Be((new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void Range_Jun30_2026()
    {
        FiscalYear.Range(6, 30, 2026).Should().Be((new DateOnly(2025, 7, 1), new DateOnly(2026, 6, 30)));
    }

    [Fact]
    public void Range_Feb28_2025_StartsOnLeapDay()
    {
        FiscalYear.Range(2, 28, 2025).Should().Be((new DateOnly(2024, 2, 29), new DateOnly(2025, 2, 28)));
    }

    [Fact]
    public void EndingYearContaining_Jun30_Aug2026_Is2027()
    {
        FiscalYear.EndingYearContaining(new DateOnly(2026, 8, 15), 6, 30).Should().Be(2027);
    }

    [Fact]
    public void EndingYearContaining_OnEndDate_IsSameYear()
    {
        FiscalYear.EndingYearContaining(new DateOnly(2026, 6, 30), 6, 30).Should().Be(2026);
    }
}
