namespace FinSight.Application.Common.Reporting;

/// <summary>Fiscal-year date arithmetic. The end month/day is never Feb 29 (rejected by the domain).</summary>
public static class FiscalYear
{
    /// <summary>The inclusive range of the fiscal year that ends in <paramref name="endingYear"/>.</summary>
    public static (DateOnly Start, DateOnly End) Range(int endMonth, int endDay, int endingYear)
    {
        var end = new DateOnly(endingYear, endMonth, endDay);
        var start = new DateOnly(endingYear - 1, endMonth, endDay).AddDays(1);
        return (start, end);
    }

    /// <summary>The year in which the fiscal year containing <paramref name="date"/> ends.</summary>
    public static int EndingYearContaining(DateOnly date, int endMonth, int endDay) =>
        date <= new DateOnly(date.Year, endMonth, endDay) ? date.Year : date.Year + 1;
}
