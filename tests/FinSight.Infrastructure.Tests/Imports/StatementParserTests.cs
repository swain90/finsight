using System.Text;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
using FinSight.Application.Common.Exceptions;
using FinSight.Infrastructure.Imports;
using FinSight.Infrastructure.Imports.Pdf;
using FinSight.Infrastructure.Tests.Imports.Pdf;
using FinSight.Domain.Common;
using FluentAssertions;

namespace FinSight.Infrastructure.Tests.Imports;

public class StatementParserTests
{
    private const string UnknownMessage =
        "Unrecognized file. Expected a CSV export from CIBC, TD, MBNA, RBC or Wealthsimple, or a CIBC credit card PDF statement.";

    private static MemoryStream Csv(string text) => new(Encoding.UTF8.GetBytes(text));

    private static StatementParser Parser(params IStatementReader[] readers) => new(readers, []);

    private static StatementParser PdfParser(params IPdfStatementReader[] readers) => new([], readers);

    private static MemoryStream Pdf() => new(TestPdf.Build((1, 50, 700, "FAKEPDF statement")));

    [Fact]
    public void Parse_KnownFile_ReturnsInstitutionAndRows()
    {
        var result = Parser(new FakeReader()).ParseCsv(Csv(
            "Date,Description,Amount\n2026-10-03,COFFEE,-4.50\n2026-10-04,PAYROLL,1000.00\n"));

        result.IsSuccess.Should().BeTrue();
        result.Value.Institution.Should().Be(Institution.Cibc);
        result.Value.Rows.Should().HaveCount(2);
        result.Value.Rows[1].Type.Should().Be(TransactionType.Credit);
        result.Value.Rows[1].LineNumber.Should().Be(3);
    }

    [Fact]
    public void Parse_BomCrlfQuotedAndBlankLines_ParsesRows()
    {
        var result = Parser(new FakeReader()).ParseCsv(Csv(
            "﻿Date,Description,Amount\r\n2026-10-03,\"LOBLAWS, TORONTO\",-82.15\r\n\r\n"));

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Rows.Should().ContainSingle().Subject;
        row.Description.Should().Be("LOBLAWS, TORONTO");
        row.Type.Should().Be(TransactionType.Debit);
        row.Amount.Should().Be(82.15m);
        row.LineNumber.Should().Be(2);
    }

    [Theory]
    [InlineData("0.00", false)]
    [InlineData("-12.00", true)]
    public void Parse_ReaderEmitsNonPositiveAmount_FailsWithFormatError(string amount, bool passAmountThrough)
    {
        var result = Parser(new FakeReader(passAmountThrough: passAmountThrough)).ParseCsv(Csv(
            $"Date,Description,Amount\n2026-10-03,REFUND,4.50\n2026-10-04,ODD,{amount}\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.Format");
        result.Error.Description.Should().Be("Row 3: Amount must be positive.");
    }

    [Fact]
    public void Parse_HeaderOnly_Fails()
    {
        var result = Parser(new FakeReader()).ParseCsv(Csv("Date,Description,Amount\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("The file contains no transactions.");
    }

    [Fact]
    public void Parse_NoReaderMatches_FailsWithUnknownFileMessage()
    {
        var result = Parser(new FakeReader(canRead: false)).ParseCsv(Csv("Date,Description,Amount\n2026-10-03,X,-1\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be(UnknownMessage);
    }

    [Fact]
    public void Parse_TwoReadersMatch_FailsWithUnknownFileMessage()
    {
        var result = Parser(new FakeReader(), new FakeReader(institution: Institution.Td))
            .ParseCsv(Csv("Date,Description,Amount\n2026-10-03,X,-1\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be(UnknownMessage);
    }

    [Fact]
    public void Parse_UsdRow_FailsNamingRow()
    {
        var result = Parser(new FakeReader()).ParseCsv(Csv(
            "Date,Description,Amount\n2026-10-03,A,-1.00\n2026-10-04,B,-2.00,USD\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Row 3: only CAD is supported.");
    }

    [Fact]
    public void Parse_ReaderFormatException_FailsNamingRow()
    {
        var result = Parser(new FakeReader()).ParseCsv(Csv(
            "Date,Description,Amount\n2026-10-03,A,-1.00\n2026-10-04,B,abc\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.Format");
        result.Error.Description.Should().Be("Row 3: invalid amount.");
    }

    [Fact]
    public void Parse_BlankLineMidFile_ReportsPhysicalLineNumbers()
    {
        var result = Parser(new FakeReader()).ParseCsv(Csv(
            "Date,Description,Amount\n2026-10-03,A,-1.00\n\n\n2026-10-04,B,-2.00\n"));

        result.Value.Rows.Select(r => r.LineNumber).Should().Equal(2, 5);
    }

    [Fact]
    public void Parse_QuotedFieldWithNewline_ReportsLineWhereRecordStarts()
    {
        var result = Parser(new FakeReader()).ParseCsv(Csv(
            "Date,Description,Amount\n2026-10-03,\"LINE ONE\nLINE TWO\",-1.00\n2026-10-04,B,-2.00\n"));

        result.Value.Rows.Select(r => r.LineNumber).Should().Equal(2, 4);
    }

    [Fact]
    public void Parse_MalformedCsv_FailsWithoutThrowing()
    {
        var act = () => Parser(new FakeReader()).ParseCsv(Csv(
            "Date,Description,Amount\n2026-10-03,\"unterminated,-1\n"));

        var result = act.Should().NotThrow().Subject;
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.Malformed");
    }

    [Fact]
    public void Parse_PdfContent_RoutesToPdfReader()
    {
        var result = PdfParser(new FakePdfReader()).Parse(Pdf(), "statement.pdf");

        result.IsSuccess.Should().BeTrue();
        result.Value.Institution.Should().Be(Institution.Cibc);
        result.Value.Rows.Should().ContainSingle();
    }

    [Fact]
    public void Parse_PdfExtensionCsvContent_FileMismatch()
    {
        var result = Parser(new FakeReader()).Parse(Csv("Date,Description,Amount\n2026-10-03,X,-1\n"), "statement.PDF");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.FileMismatch");
        result.Error.Description.Should().Be("The file's contents don't match its .pdf/.csv extension.");
    }

    [Fact]
    public void Parse_CsvExtensionPdfContent_FileMismatch()
    {
        var result = PdfParser(new FakePdfReader()).Parse(Pdf(), "statement.csv");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.FileMismatch");
    }

    [Fact]
    public void Parse_PdfContentWithNonSeekableStream_Works()
    {
        var bytes = TestPdf.Build((1, 50, 700, "FAKEPDF statement"));

        var result = PdfParser(new FakePdfReader()).Parse(new NonSeekableStream(bytes), "s.pdf");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Parse_CorruptPdf_PdfUnreadable()
    {
        var result = PdfParser(new FakePdfReader())
            .Parse(new MemoryStream("%PDF-1.4 garbage garbage"u8.ToArray()), "s.pdf");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.PdfUnreadable");
        result.Error.Description.Should().Be(
            "The PDF could not be read. Download the statement again from CIBC and retry.");
    }

    [Fact]
    public void Parse_PdfNoReader_UnknownFormatWithNewCopy()
    {
        var result = PdfParser().Parse(Pdf(), "s.pdf");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.UnknownFormat");
        result.Error.Description.Should().Be(UnknownMessage);
    }

    [Fact]
    public void Parse_PdfWithNoWords_UnknownFormat()
    {
        var result = PdfParser(new FakePdfReader())
            .Parse(new MemoryStream(TestPdf.Build()), "s.pdf");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.UnknownFormat");
    }

    [Fact]
    public void Parse_PdfReaderTotalsMismatch_TotalsMismatchCopy()
    {
        var result = PdfParser(new FakePdfReader(new StatementTotalsMismatchException(-1.5m)))
            .Parse(Pdf(), "s.pdf");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.TotalsMismatch");
        result.Error.Description.Should().Be(
            "This CIBC statement's transactions don't add up to its totals (off by $1.50). The statement layout may have changed; nothing was imported.");
    }

    [Fact]
    public void Parse_PdfReaderUnexpectedException_UnknownFormat()
    {
        var result = PdfParser(new FakePdfReader(new InvalidOperationException("reader bug")))
            .Parse(Pdf(), "s.pdf");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.UnknownFormat");
        result.Error.Description.Should().Be(UnknownMessage);
    }

    [Fact]
    public void Parse_PdfRowFormatError_PageMessage()
    {
        var result = PdfParser(new FakePdfReader(new StatementFormatException(7, 2, "Unreadable date.")))
            .Parse(Pdf(), "s.pdf");

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.Format");
        result.Error.Description.Should().Be("Page 2: Unreadable date.");
    }

    private static MemoryStream TwoPagePdf() =>
        new(TestPdf.Build((1, 50, 700, "intro"), (2, 50, 700, "FAKEPDF page two")));

    private static ParsedStatementRow PageTwoRow(decimal amount, string currency) =>
        new(2, new DateOnly(2026, 10, 3), "X", null, amount, TransactionType.Debit, currency, null,
            TransactionKind.Regular, null, "raw");

    [Fact]
    public void Parse_PdfNonPositiveAmount_ReportsPage()
    {
        var result = PdfParser(new FakePdfReader(row: PageTwoRow(0m, "CAD"))).Parse(TwoPagePdf(), "s.pdf");

        result.Error.Code.Should().Be("Import.Format");
        result.Error.Description.Should().Be("Page 2: Amount must be positive.");
    }

    [Fact]
    public void Parse_PdfNonCadRow_ReportsPage()
    {
        var result = PdfParser(new FakePdfReader(row: PageTwoRow(5m, "USD"))).Parse(TwoPagePdf(), "s.pdf");

        result.Error.Code.Should().Be("Import.Currency");
        result.Error.Description.Should().Be("Page 2: only CAD is supported.");
    }

    private sealed class NonSeekableStream(byte[] bytes) : MemoryStream(bytes)
    {
        public override bool CanSeek => false;
    }
}

internal static class StatementParserTestExtensions
{
    public static Result<ParsedStatement> ParseCsv(this StatementParser parser, Stream csv) =>
        parser.Parse(csv, "statement.csv");
}
