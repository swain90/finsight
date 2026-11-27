using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Imports;
using FinSight.Infrastructure.Imports.Readers;
using FluentAssertions;

namespace FinSight.Infrastructure.Tests.Imports.Readers;

public class WealthsimpleStatementReaderTests
{
    private const string Header = "\"date\",\"transaction\",\"description\",\"amount\",\"balance\",\"currency\"";

    private static readonly WealthsimpleStatementReader Reader = new();

    private static string Fixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Imports", "Fixtures", name));

    private static StatementParser Parser() => new([Reader], []);

    private static CsvRow Row(int line, params string[] fields) =>
        new(line, fields, string.Join(",", fields.Select(f => $"\"{f}\"")));

    private static CsvRow HeaderRow() =>
        Row(1, "date", "transaction", "description", "amount", "balance", "currency");

    private static List<ParsedStatementRow> ReadRows(params CsvRow[] dataRows) =>
        Reader.Read([HeaderRow(), .. dataRows]).ToList();

    [Fact]
    public void Read_CashFixture_ParsesExactRows()
    {
        var result = Parser().Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Fixture("wealthsimple-cash.csv"))), "statement.csv");

        result.IsSuccess.Should().BeTrue();
        result.Value.Institution.Should().Be(Institution.Wealthsimple);
        var rows = result.Value.Rows;
        rows.Should().HaveCount(8);

        var expected = new (int Line, string Date, string Description, decimal Amount, TransactionType Type,
            TransactionKind Kind, TransactionCategory? Category)[]
        {
            (2, "2026-01-01", "Interest earned", 1.11m, TransactionType.Credit, TransactionKind.Regular, TransactionCategory.InvestmentIncome),
            (3, "2026-01-03", "Direct deposit from EMPLOYER", 500.5m, TransactionType.Credit, TransactionKind.PendingTransfer, null),
            (4, "2026-01-04", "Pre-authorized Debit to ACME, INC", 20.0m, TransactionType.Debit, TransactionKind.Regular, null),
            (5, "2026-01-05", "Pre-authorized Debit to SAMPLECO", 5.0m, TransactionType.Debit, TransactionKind.Regular, null),
            (6, "2026-01-05", "Pre-authorized Debit to SAMPLECO", 5.0m, TransactionType.Debit, TransactionKind.Regular, null),
            (7, "2026-01-06", "Interac e-Transfer from FRIEND", 100.0m, TransactionType.Credit, TransactionKind.PendingTransfer, null),
            (8, "2026-01-07", "Interac e-Transfer to FRIEND", 50.0m, TransactionType.Debit, TransactionKind.PendingTransfer, null),
            (9, "2026-01-08", "Online bill payment for SAMPLE CARD (executed at 2026-01-08)", 75.25m, TransactionType.Debit, TransactionKind.PendingTransfer, null),
        };

        for (var i = 0; i < expected.Length; i++)
        {
            var e = expected[i];
            var r = rows[i];
            r.LineNumber.Should().Be(e.Line);
            r.Date.Should().Be(DateOnly.Parse(e.Date));
            r.Description.Should().Be(e.Description);
            r.Amount.Should().Be(e.Amount);
            r.Type.Should().Be(e.Type);
            r.KindHint.Should().Be(e.Kind);
            r.CategoryHint.Should().Be(e.Category);
            r.Currency.Should().Be("CAD");
            r.ExternalAccountRef.Should().BeNull();
        }
    }

    [Theory]
    [InlineData("INT", "5.0", TransactionType.Credit, TransactionKind.Regular, TransactionCategory.InvestmentIncome)]
    [InlineData("FPLINT", "5.0", TransactionType.Credit, TransactionKind.Regular, TransactionCategory.InvestmentIncome)]
    [InlineData("DIV", "5.0", TransactionType.Credit, TransactionKind.Regular, TransactionCategory.InvestmentIncome)]
    [InlineData("NRT", "-5.0", TransactionType.Debit, TransactionKind.Regular, TransactionCategory.InvestmentIncome)]
    [InlineData("BUY", "-5.0", TransactionType.Debit, TransactionKind.InvestmentTrade, null)]
    [InlineData("AFT_IN", "5.0", TransactionType.Credit, TransactionKind.PendingTransfer, null)]
    [InlineData("E_TRFIN", "5.0", TransactionType.Credit, TransactionKind.PendingTransfer, null)]
    [InlineData("E_TRFOUT", "-5.0", TransactionType.Debit, TransactionKind.PendingTransfer, null)]
    [InlineData("OBP_OUT", "-5.0", TransactionType.Debit, TransactionKind.PendingTransfer, null)]
    [InlineData("AFT_OUT", "-5.0", TransactionType.Debit, TransactionKind.Regular, null)]
    public void Read_MapsEveryCode(
        string code, string amount, TransactionType type, TransactionKind kind, TransactionCategory? category)
    {
        var row = ReadRows(Row(2, "2026-01-02", code, "Sample", amount, "10.0", "CAD")).Should().ContainSingle().Subject;

        row.Type.Should().Be(type);
        row.Amount.Should().Be(5.0m);
        row.KindHint.Should().Be(kind);
        row.CategoryHint.Should().Be(category);
    }

    [Fact]
    public void Read_ZeroAmountSecurityEvent_Skipped()
    {
        var result = Parser().Parse(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(Fixture("wealthsimple-tfsa.csv"))), "statement.csv");

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().HaveCount(4);
        result.Value.Rows.Select(r => r.Description).Should().NotContain(d =>
            d.Contains("on loan") || d.Contains("terminated") || d.Contains("Reorganization"));
        result.Value.Rows.Should().OnlyContain(r => r.Currency == "CAD");
    }

    [Fact]
    public void Read_UnknownCode_ThrowsStatementFormatException()
    {
        var act = () => ReadRows(Row(2, "2026-01-02", "WEIRD", "Sample", "5.0", "10.0", "CAD"));

        act.Should().Throw<StatementFormatException>()
            .Which.Should().Match<StatementFormatException>(e =>
                e.Line == 2 && e.Message == "Unknown Wealthsimple transaction code 'WEIRD'.");
    }

    [Fact]
    public void Read_NonZeroSecurityEvent_ThrowsStatementFormatException()
    {
        var act = () => ReadRows(Row(2, "2026-01-02", "LOAN", "Sample", "5.0", "10.0", "FUNDX"));

        act.Should().Throw<StatementFormatException>().Which.Line.Should().Be(2);
    }

    [Fact]
    public void Read_MalformedDate_ThrowsStatementFormatException()
    {
        var act = () => ReadRows(Row(3, "01/02/2026", "INT", "Sample", "5.0", "10.0", "CAD"));

        act.Should().Throw<StatementFormatException>().Which.Line.Should().Be(3);
    }

    [Fact]
    public void Read_WrongFieldCount_ThrowsStatementFormatException()
    {
        var act = () => ReadRows(Row(4, "2026-01-02", "INT", "Sample", "5.0"));

        act.Should().Throw<StatementFormatException>().Which.Line.Should().Be(4);
    }

    [Fact]
    public void CanRead_HeaderOnly_True()
    {
        Reader.CanRead([HeaderRow()]).Should().BeTrue();
        Reader.CanRead([Row(1, "DATE", "Transaction", "Description", "Amount", "Balance", "Currency")]).Should().BeTrue();
        Reader.CanRead([Row(1, "Date", "Description", "Amount")]).Should().BeFalse();
    }
}
