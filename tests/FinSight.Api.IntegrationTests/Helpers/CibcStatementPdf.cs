using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace FinSight.Api.IntegrationTests.Helpers;

/// <summary>
/// Builds made-up two-page CIBC credit card statements (card 4500 XXXX XXXX 1234) as real PDFs, at the real column
/// positions. <see cref="Build"/>: previous 500.00 - payments 400.00 + charges 108.40 = 208.40, five rows, Sep 30 to
/// Oct 29, 2026. <see cref="BuildNext"/> is the following statement: previous 208.40 - payments 208.40 + charges
/// 52.50 = 52.50, three rows, Oct 30 to Nov 29, 2026, with a charge dated Oct 28 (two days before its period start).
/// </summary>
public static class CibcStatementPdf
{
    public const int RowCount = 5;
    public const decimal ClosingBalance = 208.40m;
    public static readonly DateOnly PeriodEnd = new(2026, 10, 29);

    public const int NextRowCount = 3;
    public const decimal NextClosingBalance = 52.50m;
    public static readonly DateOnly NextPeriodEnd = new(2026, 11, 29);

    private const double RowHeight = 13.2;

    private sealed record Row(string Trans, string Post, string Description, string? Category, string Amount);

    private static double AmountX(string amount) => 539.8 - (amount.Length * 5.56);

    private static IEnumerable<(int, double, double, string)> ColumnByColumn(double top, IReadOnlyList<Row> rows)
    {
        for (var i = 0; i < rows.Count; i++)
            yield return (2, 36.7, top - (i * RowHeight), rows[i].Trans);
        for (var i = 0; i < rows.Count; i++)
            yield return (2, 78.7, top - (i * RowHeight), rows[i].Post);
        for (var i = 0; i < rows.Count; i++)
            yield return (2, 122.8, top - (i * RowHeight), rows[i].Description);
        for (var i = 0; i < rows.Count; i++)
            if (rows[i].Category is { } category)
                yield return (2, 341.5, top - (i * RowHeight), category);
        for (var i = 0; i < rows.Count; i++)
            yield return (2, AmountX(rows[i].Amount), top - (i * RowHeight), rows[i].Amount);
    }

    public static byte[] Build() => BuildStatement(
        "Transactions from September 30 to October 29, 2026", "$500.00", "$208.40",
        [
            new("Oct 07", "Oct 08", "PAYMENT THANK YOU/PAIEMENT MERCI", null, "250.00"),
            new("Oct 20", "Oct 21", "PAYMENT THANK YOU/PAIEMENT MERCI", null, "150.00"),
        ],
        "$400.00",
        [
            new("Sep 29", "Oct 01", "CORNER CAFE SAMPLETOWN ZZ", "Restaurants", "18.40"),
            new("Oct 02", "Oct 03", "MAPLE GROCER SAMPLETOWN ZZ", "Retail and Grocery", "96.15"),
            new("Oct 09", "Oct 10", "MAPLE GROCER SAMPLETOWN ZZ", "Retail and Grocery", "-6.15"),
        ],
        "$108.40");

    public static byte[] BuildNext() => BuildStatement(
        "Transactions from October 30 to November 29, 2026", "$208.40", "$52.50",
        [
            new("Nov 10", "Nov 12", "PAYMENT THANK YOU/PAIEMENT MERCI", null, "208.40"),
        ],
        "$208.40",
        [
            new("Oct 28", "Oct 30", "CORNER CAFE SAMPLETOWN ZZ", "Restaurants", "12.50"),
            new("Nov 05", "Nov 06", "MAPLE GROCER SAMPLETOWN ZZ", "Retail and Grocery", "40.00"),
        ],
        "$52.50");

    private static byte[] BuildStatement(string periodLine, string previousBalance, string totalBalance,
        Row[] payments, string totalPayments, Row[] charges, string totalCharges)
    {
        var items = new List<(int, double, double, string)>
        {
            // Page 1: summary.
            (1, 72.7, 684.5, "CIBC Dividend Visa Infinite Card"),
            (1, 418.3, 682.1, "Account number"),
            (1, 418.3, 671.3, "4500 XXXX XXXX 1234"),
            (1, 72.7, 613.2, "Previous balance"),
            (1, 337.4, 613.2, previousBalance),
            (1, 72.7, 483.4, "Total balance ="),
            (1, 337.4, 483.4, totalBalance),
            (1, 133.2, 446.4, "1"),
            // Page 2: transactions.
            (2, 36.7, 746.0, periodLine),
            (2, 36.7, 727.7, "Your payments"),
            (2, 36.7, 714.8, "Trans"),
            (2, 78.7, 714.8, "Post"),
            (2, 36.7, 706.8, "date"),
            (2, 78.7, 706.8, "date"),
            (2, 118.8, 706.8, "Description"),
            (2, 504.2, 706.8, "Amount($)"),
            (2, 36.7, 660.0, "Total payments"),
            (2, 504.9, 660.0, totalPayments),
            (2, 36.7, 643.7, "Your new charges and credits"),
            (2, 36.7, 602.0, "Trans"),
            (2, 78.7, 602.0, "Post"),
            (2, 36.7, 594.0, "date"),
            (2, 78.7, 594.0, "date"),
            (2, 122.4, 594.0, "Description"),
            (2, 325.4, 594.0, "Spend Categories"),
            (2, 504.5, 594.0, "Amount($)"),
            (2, 36.7, 581.3, "Card number 4500 XXXX XXXX 1234"),
            (2, 36.7, 500.0, "Total for 4500 XXXX XXXX 1234"),
            (2, 504.9, 500.0, totalCharges),
            (2, 206.4, 83.6, "2"),
        };
        items.AddRange(ColumnByColumn(697.5, payments));
        items.AddRange(ColumnByColumn(565.7, charges));
        return Build([.. items]);
    }

    private static byte[] Build(params (int page, double x, double y, string text)[] items)
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var pageCount = items.Max(i => i.page);
        var pages = Enumerable.Range(0, pageCount).Select(_ => builder.AddPage(PageSize.A4)).ToList();
        foreach (var (page, x, y, text) in items)
            pages[page - 1].AddText(text, 10, new PdfPoint(x, y), font);
        return builder.Build();
    }
}
