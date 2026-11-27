using FinSight.Domain.Enums;
using FinSight.Infrastructure.Imports;
using FinSight.Infrastructure.Imports.Readers;
using FluentAssertions;

namespace FinSight.Infrastructure.Tests.Imports.Pdf;

/// <summary>A made-up two-page CIBC statement written as a real PDF and parsed through <see cref="StatementParser"/>.</summary>
public class CibcPdfEndToEndTests
{
    private const double RowHeight = 13.2;

    private sealed record Row(string Trans, string Post, string Description, string? Category, string Amount);

    /// <summary>Right-aligns an amount so it ends at the real amount column edge (Helvetica 10pt digits are ~5.56pt wide).</summary>
    private static double AmountX(string amount) => 539.8 - (amount.Length * 5.56);

    /// <summary>Emits rows column by column — every date, then every description, then every amount — like CIBC's content stream.</summary>
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

    // The cash-back marker isn't emitted here: PdfPig's standard-14 writer can't encode it (the reader tests cover it).
    [Fact]
    public void CibcPdf_BuiltDocument_ParsesThroughStatementParser()
    {
        Row[] payments =
        [
            new("Oct 07", "Oct 08", "PAYMENT THANK YOU/PAIEMENT MERCI", null, "250.00"),
            new("Oct 20", "Oct 21", "PAYMENT THANK YOU/PAIEMENT MERCI", null, "150.00"),
        ];
        Row[] charges =
        [
            new("Sep 29", "Oct 01", "CORNER CAFE TORONTO ON", "Restaurants", "18.40"),
            new("Oct 02", "Oct 03", "MAPLE GROCER TORONTO ON", "Retail and Grocery", "96.15"),
            new("Oct 09", "Oct 10", "MAPLE GROCER TORONTO ON", "Retail and Grocery", "-6.15"),
        ];

        var items = new List<(int, double, double, string)>
        {
            // Page 1: summary.
            (1, 72.7, 684.5, "CIBC Dividend Visa Infinite Card"),
            (1, 418.3, 682.1, "Account number"),
            (1, 418.3, 671.3, "4500 XXXX XXXX 1234"),
            (1, 72.7, 613.2, "Previous balance"),
            (1, 337.4, 613.2, "$500.00"),
            (1, 72.7, 483.4, "Total balance ="),
            (1, 337.4, 483.4, "$208.40"),
            (1, 133.2, 446.4, "1"),
            // Page 2: transactions.
            (2, 36.7, 746.0, "Transactions from September 28 to October 27, 2026"),
            (2, 36.7, 727.7, "Your payments"),
            (2, 36.7, 714.8, "Trans"),
            (2, 78.7, 714.8, "Post"),
            (2, 36.7, 706.8, "date"),
            (2, 78.7, 706.8, "date"),
            (2, 118.8, 706.8, "Description"),
            (2, 504.2, 706.8, "Amount($)"),
            (2, 36.7, 660.0, "Total payments"),
            (2, 504.9, 660.0, "$400.00"),
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
            (2, 504.9, 500.0, "$108.40"),
            (2, 206.4, 83.6, "2"),
        };
        items.AddRange(ColumnByColumn(697.5, payments));
        items.AddRange(ColumnByColumn(565.7, charges));
        var pdf = TestPdf.Build([.. items]);

        var result = new StatementParser([], [new CibcCreditCardPdfReader()])
            .Parse(new MemoryStream(pdf), "statement.pdf");

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error.Description : null);
        result.Value.Institution.Should().Be(Institution.Cibc);
        result.Value.Rows.Should().HaveCount(5);
        result.Value.Rows.Select(r => r.Description).Should().Equal(
            "PAYMENT THANK YOU/PAIEMENT MERCI", "PAYMENT THANK YOU/PAIEMENT MERCI",
            "CORNER CAFE TORONTO ON", "MAPLE GROCER TORONTO ON", "MAPLE GROCER TORONTO ON");
        result.Value.Rows[2].CategoryHint.Should().Be(TransactionCategory.DiningOut);
        result.Value.Rows[4].Type.Should().Be(TransactionType.Credit);
        result.Value.Summary.Should().Be(new Application.Common.Interfaces.StatementSummary(
            new DateOnly(2026, 9, 28), new DateOnly(2026, 10, 27), 500.00m, 208.40m, "1234"));
    }
}
