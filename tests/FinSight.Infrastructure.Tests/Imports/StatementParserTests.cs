using System.Text;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Imports;
using FluentAssertions;

namespace FinSight.Infrastructure.Tests.Imports;

public class StatementParserTests
{
    private const string UnknownMessage =
        "Unrecognized file. Expected a CSV export from CIBC, TD, MBNA, RBC or Wealthsimple.";

    private static MemoryStream Csv(string text) => new(Encoding.UTF8.GetBytes(text));

    private static StatementParser Parser(params IStatementReader[] readers) => new(readers);

    [Fact]
    public void Parse_KnownFile_ReturnsInstitutionAndRows()
    {
        var result = Parser(new FakeReader()).Parse(Csv(
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
        var result = Parser(new FakeReader()).Parse(Csv(
            "﻿Date,Description,Amount\r\n2026-10-03,\"LOBLAWS, TORONTO\",-82.15\r\n\r\n"));

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Rows.Should().ContainSingle().Subject;
        row.Description.Should().Be("LOBLAWS, TORONTO");
        row.Type.Should().Be(TransactionType.Debit);
        row.Amount.Should().Be(82.15m);
        row.LineNumber.Should().Be(2);
    }

    [Fact]
    public void Parse_HeaderOnly_Fails()
    {
        var result = Parser(new FakeReader()).Parse(Csv("Date,Description,Amount\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("The file contains no transactions.");
    }

    [Fact]
    public void Parse_NoReaderMatches_FailsWithUnknownFileMessage()
    {
        var result = Parser(new FakeReader(canRead: false)).Parse(Csv("Date,Description,Amount\n2026-10-03,X,-1\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be(UnknownMessage);
    }

    [Fact]
    public void Parse_TwoReadersMatch_FailsWithUnknownFileMessage()
    {
        var result = Parser(new FakeReader(), new FakeReader(institution: Institution.Td))
            .Parse(Csv("Date,Description,Amount\n2026-10-03,X,-1\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be(UnknownMessage);
    }

    [Fact]
    public void Parse_UsdRow_FailsNamingRow()
    {
        var result = Parser(new FakeReader()).Parse(Csv(
            "Date,Description,Amount\n2026-10-03,A,-1.00\n2026-10-04,B,-2.00,USD\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Description.Should().Be("Row 3: only CAD is supported.");
    }

    [Fact]
    public void Parse_ReaderFormatException_FailsNamingRow()
    {
        var result = Parser(new FakeReader()).Parse(Csv(
            "Date,Description,Amount\n2026-10-03,A,-1.00\n2026-10-04,B,abc\n"));

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.Format");
        result.Error.Description.Should().Be("Row 3: invalid amount.");
    }

    [Fact]
    public void Parse_BlankLineMidFile_ReportsPhysicalLineNumbers()
    {
        var result = Parser(new FakeReader()).Parse(Csv(
            "Date,Description,Amount\n2026-10-03,A,-1.00\n\n\n2026-10-04,B,-2.00\n"));

        result.Value.Rows.Select(r => r.LineNumber).Should().Equal(2, 5);
    }

    [Fact]
    public void Parse_QuotedFieldWithNewline_ReportsLineWhereRecordStarts()
    {
        var result = Parser(new FakeReader()).Parse(Csv(
            "Date,Description,Amount\n2026-10-03,\"LINE ONE\nLINE TWO\",-1.00\n2026-10-04,B,-2.00\n"));

        result.Value.Rows.Select(r => r.LineNumber).Should().Equal(2, 4);
    }

    [Fact]
    public void Parse_MalformedCsv_FailsWithoutThrowing()
    {
        var act = () => Parser(new FakeReader()).Parse(Csv(
            "Date,Description,Amount\n2026-10-03,\"unterminated,-1\n"));

        var result = act.Should().NotThrow().Subject;
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.Malformed");
    }
}
