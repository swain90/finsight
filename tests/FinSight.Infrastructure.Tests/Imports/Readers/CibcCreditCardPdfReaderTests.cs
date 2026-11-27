using System.Globalization;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Imports.Pdf;
using FinSight.Infrastructure.Imports.Readers;
using FluentAssertions;

namespace FinSight.Infrastructure.Tests.Imports.Readers;

/// <summary>
/// Synthetic CIBC credit card statements laid out at the real column positions (PDF points from the left).
/// Every merchant, amount and date is made up.
/// </summary>
public class CibcCreditCardPdfReaderTests
{
    private static readonly CibcCreditCardPdfReader Reader = new();

    private const string Card = "4500 XXXX XXXX 1234";
    private const string PaymentText = "PAYMENT THANK YOU/PAIEMENT MERCI";
    private const string Period = "September 30 to October 29, 2026";

    /// <summary>Builds <see cref="PdfText"/> line by line; odd-numbered pages after page 1 shift every column right by 36pt.</summary>
    private sealed class Doc
    {
        private readonly List<PdfLine> _lines = [];
        private int _page = 1;
        private double _y = 760;

        public double Offset => _page > 1 && _page % 2 == 1 ? 36 : 0;

        public PdfText Text => new(_lines);

        public Doc NewPage()
        {
            _page++;
            _y = 760;
            return this;
        }

        public Doc Gap(double points)
        {
            _y -= points;
            return this;
        }

        /// <summary>One line; each run's words start at its x (relative to the page's column offset).</summary>
        public Doc Line(params (double X, string Text)[] runs)
        {
            var words = new List<PdfWord>();
            foreach (var (x, text) in runs)
            {
                var left = x + Offset;
                foreach (var w in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    words.Add(new PdfWord(w, left, left + (w.Length * 4.4)));
                    left += (w.Length * 4.4) + 2.2;
                }
            }
            _lines.Add(new PdfLine(_page, _lines.Count + 1, _y, words.OrderBy(w => w.Left).ToList()));
            _y -= 13.2;
            return this;
        }

        public int LineOf(string fragment) => _lines.First(l => l.Text.Contains(fragment)).LineNumber;

        // ─── Page 1 summary ───
        public Doc Summary(string previous, string total, string period = Period, string card = Card) =>
            Line((72.7, "CIBC Dividend Visa Infinite Card"))
                .Line((418.3, "Account number"))
                .Line((418.3, card))
                .Line((418.3, period))
                .Line((72.7, "Previous balance"), (337.4, previous))
                .Line((90.0, "Payments"), (240.0, "$0.00"))
                .Line((72.7, "Total balance ="), (337.4, total))
                .Line((133.2, "1"))
                .Line((72.7, "Amount Due"));

        public Doc PeriodLine(string period = Period) => Line((36.7, $"Transactions from {period}"));

        // ─── Sections ───
        public Doc PaymentsHeader() =>
            Line((36.7, "Your payments"))
                .Line((36.7, "Trans"), (78.7, "Post"))
                .Line((36.7, "date"), (78.7, "date"), (118.8, "Description"), (504.2, "Amount($)"));

        public Doc Payment(string trans, string post, string amount) =>
            Line((36.7, trans), (78.7, post), (118.8, PaymentText), Amount(amount));

        public Doc InterestHeader() =>
            Line((36.7, "Your interest"))
                .Line((36.7, "Trans"), (76.8, "Post"))
                .Line((36.7, "date"), (76.8, "date"), (118.8, "Description"), (299.5, "Annual interest rate"), (504.2, "Amount($)"));

        public Doc Interest(string trans, string post, string description, string amount) =>
            Line((36.7, trans), (78.7, post), (118.8, description), (338.1, "19.99%"), Amount(amount));

        /// <summary>Interest reversals print further left than the amount column, ending at x≈487.</summary>
        public Doc InterestReversal(string trans, string post, string amount) =>
            Line((36.7, trans), (78.7, post), (118.8, "INTEREST REVERSAL"), (487.2 - (amount.Length * 4.4), amount));

        public Doc ChargesHeader(bool continued = false) =>
            Line((36.7, continued ? "Your new charges and credits (continued)" : "Your new charges and credits"))
                .Line((39.6, "Ý Identifies Cash Back Multiplier transactions that have earned enhanced cash back"))
                .Line((36.7, "Trans"), (78.7, "Post"))
                .Line((36.7, "date"), (78.7, "date"), (122.4, "Description"), (325.4, "Spend Categories"), (504.5, "Amount($)"));

        public Doc CardNumber(string card = Card) => Line((36.7, $"Card number {card}"));

        public Doc Charge(string trans, string post, string description, string category, string amount, bool cashBack = false)
        {
            var runs = new List<(double, string)> { (36.7, trans), (78.7, post) };
            if (cashBack) runs.Add((109.0, "Ý"));
            runs.Add((122.8, description));
            runs.Add((341.5, category));
            runs.Add(Amount(amount));
            return Line([.. runs]);
        }

        public Doc TotalPayments(string amount) => Line((36.7, "Total payments"), (504.9, amount));

        public Doc TotalInterest(string amount) => Line((36.7, "Total interest this period"), (520.0, amount));

        public Doc TotalFor(string amount, string card = Card) => Line((36.7, $"Total for {card}"), (504.9, amount));

        public Doc PageFooter(int n) => Gap(20).Line((481.7, $"Page {n} of 5"));

        private static (double, string) Amount(string amount) => (539.8 - (amount.Length * 4.4), amount);
    }

    /// <summary>2 payments, 1 interest charge, 3 charges (one Restaurants) and 1 refund that all reconcile.</summary>
    private static Doc BasicStatement(string totalPayments = "$500.00", string totalInterest = "$4.10",
        string totalFor = "$1,122.10", string totalBalance = "$1,626.20") =>
        new Doc()
            .Summary("$1,000.00", totalBalance)
            .NewPage()
            .PeriodLine()
            .PaymentsHeader()
            .Payment("Oct 07", "Oct 08", "300.00")
            .Payment("Oct 15", "Oct 16", "200.00")
            .TotalPayments(totalPayments)
            .InterestHeader()
            .Interest("Oct 27", "Oct 27", "PURCHASES", "4.10")
            .TotalInterest(totalInterest)
            .ChargesHeader()
            .CardNumber()
            .Charge("Oct 01", "Oct 02", "MAPLE GROCER SAMPLETOWN ZZ", "Retail and Grocery", "45.67")
            .Charge("Oct 03", "Oct 04", "THE BISTRO SAMPLETOWN ZZ", "Restaurants", "32.10")
            .Charge("Oct 05", "Oct 06", "ACME FURNITURE CO SAMPLETOWN ZZ", "Home and Office Improvement", "1,050.00")
            .Charge("Oct 10", "Oct 11", "MAPLE GROCER SAMPLETOWN ZZ", "Retail and Grocery", "-5.67")
            .TotalFor(totalFor);

    private static ParsedStatementRow Row(int line, DateOnly date, string description, decimal amount, TransactionType type,
        TransactionKind kind, TransactionCategory? category, string rawLine) =>
        new(line, date, description, null, amount, type, "CAD", "1234", kind, category, rawLine);

    [Fact]
    public void Read_BasicStatement_ParsesExactRowsAndSummary()
    {
        var doc = BasicStatement();

        var statement = Reader.Read(doc.Text);

        statement.Institution.Should().Be(Institution.Cibc);
        statement.Summary.Should().Be(new StatementSummary(
            new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 29), 1000.00m, 1626.20m, "1234"));
        statement.Rows.Should().Equal(
            Row(doc.LineOf("Oct 07"), new(2026, 10, 7), PaymentText, 300.00m, TransactionType.Credit,
                TransactionKind.PendingTransfer, TransactionCategory.DebtPayments, $"Oct 07 Oct 08 {PaymentText} 300.00"),
            Row(doc.LineOf("Oct 15"), new(2026, 10, 15), PaymentText, 200.00m, TransactionType.Credit,
                TransactionKind.PendingTransfer, TransactionCategory.DebtPayments, $"Oct 15 Oct 16 {PaymentText} 200.00"),
            Row(doc.LineOf("PURCHASES"), new(2026, 10, 27), "PURCHASES", 4.10m, TransactionType.Debit,
                TransactionKind.Regular, TransactionCategory.BankFees, "Oct 27 Oct 27 PURCHASES 19.99% 4.10"),
            Row(doc.LineOf("Oct 01"), new(2026, 10, 1), "MAPLE GROCER SAMPLETOWN ZZ", 45.67m, TransactionType.Debit,
                TransactionKind.Regular, null, "Oct 01 Oct 02 MAPLE GROCER SAMPLETOWN ZZ Retail and Grocery 45.67"),
            Row(doc.LineOf("THE BISTRO"), new(2026, 10, 3), "THE BISTRO SAMPLETOWN ZZ", 32.10m, TransactionType.Debit,
                TransactionKind.Regular, TransactionCategory.DiningOut, "Oct 03 Oct 04 THE BISTRO SAMPLETOWN ZZ Restaurants 32.10"),
            Row(doc.LineOf("ACME FURNITURE"), new(2026, 10, 5), "ACME FURNITURE CO SAMPLETOWN ZZ", 1050.00m, TransactionType.Debit,
                TransactionKind.Regular, null, "Oct 05 Oct 06 ACME FURNITURE CO SAMPLETOWN ZZ Home and Office Improvement 1,050.00"),
            Row(doc.LineOf("Oct 10"), new(2026, 10, 10), "MAPLE GROCER SAMPLETOWN ZZ", 5.67m, TransactionType.Credit,
                TransactionKind.Regular, null, "Oct 10 Oct 11 MAPLE GROCER SAMPLETOWN ZZ Retail and Grocery -5.67"));
    }

    [Fact]
    public void Read_TransportationCategory_HintsTransportation()
    {
        var doc = new Doc()
            .Summary("$0.00", "$12.00")
            .NewPage().PeriodLine()
            .ChargesHeader().CardNumber()
            .Charge("Oct 05", "Oct 06", "ACME BUSLINES SAMPLETOWN ZZ", "Transportation", "12.00")
            .TotalFor("$12.00");

        Reader.Read(doc.Text).Rows.Should().ContainSingle()
            .Which.CategoryHint.Should().Be(TransactionCategory.Transportation);
    }

    [Fact]
    public void Read_ContinuedSectionAcrossPages_AllRowsParsed()
    {
        var doc = new Doc()
            .Summary("$0.00", "$60.00")
            .NewPage().PeriodLine()
            .ChargesHeader().CardNumber()
            .Charge("Oct 01", "Oct 02", "ALPHA STORE SAMPLETOWN ZZ", "Retail and Grocery", "10.00")
            .Charge("Oct 02", "Oct 03", "BETA STORE SAMPLETOWN ZZ", "Retail and Grocery", "20.00")
            .PageFooter(2)
            .NewPage() // odd page: every column shifts right
            .Line((72.7, "Prepared for: A CARDHOLDER September 30 to October 29, 2026 Account number:"), (416.4, Card))
            .ChargesHeader(continued: true).CardNumber()
            .Charge("Oct 03", "Oct 04", "GAMMA STORE SAMPLETOWN ZZ", "Retail and Grocery", "30.00")
            .TotalFor("$60.00");

        var rows = Reader.Read(doc.Text).Rows;

        rows.Select(r => r.Description).Should().Equal("ALPHA STORE SAMPLETOWN ZZ", "BETA STORE SAMPLETOWN ZZ", "GAMMA STORE SAMPLETOWN ZZ");
        rows.Select(r => r.Amount).Should().Equal(10.00m, 20.00m, 30.00m);
        rows[1].RawLine.Should().NotContain("Page");
        rows[2].CategoryHint.Should().BeNull();
    }

    [Fact]
    public void Read_TwoCardSubsections_EachTotalChecked()
    {
        const string second = "4500 XXXX XXXX 9876";
        var doc = new Doc()
            .Summary("$0.00", "$35.00")
            .NewPage().PeriodLine()
            .ChargesHeader()
            .CardNumber()
            .Charge("Oct 01", "Oct 02", "ALPHA STORE SAMPLETOWN ZZ", "Retail and Grocery", "10.00")
            .TotalFor("$10.00")
            .CardNumber(second)
            .Charge("Oct 02", "Oct 03", "BETA STORE SAMPLETOWN ZZ", "Retail and Grocery", "25.00")
            .TotalFor("$25.00", "9876");

        Reader.Read(doc.Text).Rows.Select(r => r.Amount).Should().Equal(10.00m, 25.00m);

        var swapped = new Doc()
            .Summary("$0.00", "$35.00")
            .NewPage().PeriodLine()
            .ChargesHeader()
            .CardNumber()
            .Charge("Oct 01", "Oct 02", "ALPHA STORE SAMPLETOWN ZZ", "Retail and Grocery", "10.00")
            .TotalFor("$25.00")
            .CardNumber(second)
            .Charge("Oct 02", "Oct 03", "BETA STORE SAMPLETOWN ZZ", "Retail and Grocery", "25.00")
            .TotalFor("$10.00", second);

        var act = () => Reader.Read(swapped.Text);

        act.Should().Throw<StatementTotalsMismatchException>().Which.Difference.Should().Be(-15.00m);
    }

    [Fact]
    public void Read_CashBackMarker_StrippedFromDescription()
    {
        var doc = new Doc()
            .Summary("$0.00", "$8.25")
            .NewPage().PeriodLine()
            .ChargesHeader().CardNumber()
            .Charge("Oct 04", "Oct 05", "CORNER CAFE SAMPLETOWN ZZ", "Restaurants", "8.25", cashBack: true)
            .TotalFor("$8.25");

        var row = Reader.Read(doc.Text).Rows.Should().ContainSingle().Subject;

        row.Description.Should().Be("CORNER CAFE SAMPLETOWN ZZ");
        row.CategoryHint.Should().Be(TransactionCategory.DiningOut);
    }

    [Fact]
    public void Read_ForeignCurrencyLine_InRawLineNotDescription()
    {
        var doc = new Doc()
            .Summary("$0.00", "$70.30")
            .NewPage().PeriodLine()
            .ChargesHeader().CardNumber()
            .Charge("Oct 11", "Oct 13", "WEB STORE SAMPLEVILLE ZZ", "Foreign Currency Transactions", "70.30")
            .Gap(-2.2).Line((122.8, "50.00 USD @ 1.406000000**"))
            .TotalFor("$70.30");

        var row = Reader.Read(doc.Text).Rows.Should().ContainSingle().Subject;

        row.Description.Should().Be("WEB STORE SAMPLEVILLE ZZ");
        row.Amount.Should().Be(70.30m);
        row.RawLine.Should().Be("Oct 11 Oct 13 WEB STORE SAMPLEVILLE ZZ Foreign Currency Transactions 70.30 50.00 USD @ 1.406000000**");
    }

    [Fact]
    public void Read_InterestReversalTripleStar_CreditExcludedFromInterestTotalIncludedInBalance()
    {
        // 100.00 + 3.00 interest - 1.25 reversal = 101.75; "Total interest this period" shows only the 3.00.
        var doc = new Doc()
            .Summary("$100.00", "$101.75")
            .NewPage().PeriodLine()
            .InterestHeader()
            .InterestReversal("Aug 27", "Oct 04", "-1.25***")
            .Interest("Oct 27", "Oct 27", "CASH ADVANCES", "3.00")
            .TotalInterest("$3.00")
            .Line((36.7, "*** The interest adjustments above are not included in the \"Total Interest this period\""));

        var rows = Reader.Read(doc.Text).Rows;

        rows.Should().HaveCount(2);
        rows[0].Should().Be(Row(doc.LineOf("REVERSAL"), new(2026, 8, 27), "INTEREST REVERSAL", 1.25m,
            TransactionType.Credit, TransactionKind.Regular, TransactionCategory.BankFees, "Aug 27 Oct 04 INTEREST REVERSAL -1.25***"));
        rows[1].Type.Should().Be(TransactionType.Debit);
        rows[1].CategoryHint.Should().Be(TransactionCategory.BankFees);
    }

    [Theory]
    [InlineData("December 30, 2025 to January 29, 2026")]
    [InlineData("December 30 , 2025 to January 29, 2026")]
    public void Read_DecJanPeriod_AssignsPreviousYearToDecember(string period)
    {
        var doc = new Doc()
            .Summary("$0.00", "$30.00", period)
            .NewPage().PeriodLine(period)
            .ChargesHeader().CardNumber()
            .Charge("Dec 24", "Dec 28", "HOLIDAY STORE SAMPLETOWN ZZ", "Retail and Grocery", "10.00")
            .Charge("Jan 05", "Jan 06", "NEW YEAR STORE SAMPLETOWN ZZ", "Retail and Grocery", "20.00")
            .TotalFor("$30.00");

        var statement = Reader.Read(doc.Text);

        statement.Rows.Select(r => r.Date).Should().Equal(new DateOnly(2025, 12, 24), new DateOnly(2026, 1, 5));
        statement.Summary!.PeriodStart.Should().Be(new DateOnly(2025, 12, 30));
        statement.Summary.PeriodEnd.Should().Be(new DateOnly(2026, 1, 29));
    }

    [Fact]
    public void Read_RowWithSingleDate_UsesThatDate()
    {
        var doc = new Doc()
            .Summary("$0.00", "$9.99")
            .NewPage().PeriodLine()
            .ChargesHeader().CardNumber()
            .Line((36.7, "Oct 09"), (122.8, "STREAMING SVC SAMPLETOWN ZZ"), (341.5, "Hotel, Entertainment and Recreation"), (519.6, "9.99"))
            .TotalFor("$9.99");

        var row = Reader.Read(doc.Text).Rows.Should().ContainSingle().Subject;

        row.Date.Should().Be(new DateOnly(2026, 10, 9));
        row.Description.Should().Be("STREAMING SVC SAMPLETOWN ZZ");
    }

    [Fact]
    public void Read_NoInterestSection_Balances()
    {
        var doc = new Doc()
            .Summary("$200.00", "$125.00")
            .NewPage().PeriodLine()
            .PaymentsHeader()
            .Payment("Oct 07", "Oct 08", "200.00")
            .TotalPayments("$200.00")
            .ChargesHeader().CardNumber()
            .Charge("Oct 01", "Oct 02", "ALPHA STORE SAMPLETOWN ZZ", "Retail and Grocery", "125.00")
            .TotalFor("$125.00");

        Reader.Read(doc.Text).Rows.Should().HaveCount(2);
    }

    [Fact]
    public void Read_StrayDigitLines_NotRowsAndNotAppended()
    {
        var doc = new Doc()
            .Summary("$0.00", "$10.00")
            .NewPage().PeriodLine()
            .ChargesHeader().CardNumber()
            .Charge("Oct 01", "Oct 02", "ALPHA STORE SAMPLETOWN ZZ", "Retail and Grocery", "10.00")
            .Gap(30).Line((206.4, "2"))
            .Gap(30).Line((481.7, "Page 2 of 5"))
            .NewPage().Line((36.7, "Your new charges and credits (continued)"))
            .Line((206.4, "3"))
            .TotalFor("$10.00");

        var row = Reader.Read(doc.Text).Rows.Should().ContainSingle().Subject;

        row.RawLine.Should().Be("Oct 01 Oct 02 ALPHA STORE SAMPLETOWN ZZ Retail and Grocery 10.00");
    }

    [Fact]
    public void Read_PaymentsTotalWrong_ThrowsMismatchWithDifference()
    {
        var act = () => Reader.Read(BasicStatement(totalPayments: "$510.00").Text);

        act.Should().Throw<StatementTotalsMismatchException>().Which.Difference.Should().Be(-10.00m);
    }

    [Fact]
    public void Read_InterestTotalWrong_ThrowsMismatchWithDifference()
    {
        var act = () => Reader.Read(BasicStatement(totalInterest: "$4.00").Text);

        act.Should().Throw<StatementTotalsMismatchException>().Which.Difference.Should().Be(0.10m);
    }

    [Fact]
    public void Read_CardTotalWrong_ThrowsMismatchWithDifference()
    {
        var act = () => Reader.Read(BasicStatement(totalFor: "$1,122.00").Text);

        act.Should().Throw<StatementTotalsMismatchException>().Which.Difference.Should().Be(0.10m);
    }

    [Fact]
    public void Read_BalanceEquationWrong_ThrowsMismatchWithDifference()
    {
        var act = () => Reader.Read(BasicStatement(totalBalance: "$1,630.20").Text);

        act.Should().Throw<StatementTotalsMismatchException>().Which.Difference.Should().Be(-4.00m);
    }

    [Fact]
    public void Read_MissingSummary_ThrowsFormatException()
    {
        var doc = new Doc()
            .Line((72.7, "CIBC Dividend Visa Infinite Card"))
            .Line((418.3, $"Account number {Card}"))
            .NewPage().PeriodLine()
            .ChargesHeader().CardNumber()
            .Charge("Oct 01", "Oct 02", "ALPHA STORE SAMPLETOWN ZZ", "Retail and Grocery", "10.00")
            .TotalFor("$10.00");

        var act = () => Reader.Read(doc.Text);

        var ex = act.Should().Throw<StatementFormatException>().Which;
        ex.Message.Should().Be("Statement summary not found.");
        ex.Line.Should().Be(1);
        ex.Page.Should().Be(1);
    }

    [Fact]
    public void CanRead_CibcStatement_True() =>
        Reader.CanRead(BasicStatement().Text).Should().BeTrue();

    [Fact]
    public void CanRead_OtherPdf_False()
    {
        var other = new Doc()
            .Line((72.7, "Annual Report on Charges and Compensation"))
            .Line((72.7, "Account number WS123"))
            .Line((72.7, "Period January 1 to December 31, 2025"));

        Reader.CanRead(other.Text).Should().BeFalse();
        Reader.Institution.Should().Be(Institution.Cibc);
    }

    [Fact]
    public void Read_AmountsUseInvariantCulture()
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("fr-CA");
        try
        {
            Reader.Read(BasicStatement().Text).Summary!.ClosingBalance.Should().Be(1626.20m);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
