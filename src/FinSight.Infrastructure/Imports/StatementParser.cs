using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.ValueObjects;

namespace FinSight.Infrastructure.Imports;

public sealed class StatementParser(IEnumerable<IStatementReader> readers) : IStatementParser
{
    private const string UnknownFileMessage =
        "Unrecognized file. Expected a CSV export from CIBC, TD, MBNA, RBC or Wealthsimple.";

    private const int SniffRowCount = 5;

    public Result<ParsedStatement> Parse(Stream csv)
    {
        var rows = ReadRows(csv);
        var firstRows = rows.Take(SniffRowCount).ToList();

        var matches = readers.Where(r => r.CanRead(firstRows)).ToList();
        if (matches.Count != 1)
            return Result.Failure<ParsedStatement>(Error.Custom("Import.UnknownFormat", UnknownFileMessage));

        var reader = matches[0];
        List<ParsedStatementRow> parsed;
        try
        {
            parsed = reader.Read(rows).ToList();
        }
        catch (StatementFormatException ex)
        {
            return Result.Failure<ParsedStatement>(
                Error.Custom("Import.Format", $"Row {ex.Line}: {ex.Message}"));
        }

        if (parsed.Count == 0)
            return Result.Failure<ParsedStatement>(
                Error.Custom("Import.Empty", "The file contains no transactions."));

        var nonCad = parsed.FirstOrDefault(r =>
            !string.Equals(r.Currency, Money.DefaultCurrency, StringComparison.OrdinalIgnoreCase));
        if (nonCad is not null)
            return Result.Failure<ParsedStatement>(
                Error.Custom("Import.Currency", $"Row {nonCad.LineNumber}: only CAD is supported."));

        return Result.Success(new ParsedStatement(reader.Institution, parsed));
    }

    private static List<CsvRow> ReadRows(Stream csv)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            TrimOptions = TrimOptions.Trim,
            DetectDelimiter = false,
            BadDataFound = null,
            MissingFieldFound = null,
            ShouldSkipRecord = args => args.Row.ColumnCount == 0
                || args.Row.Parser.Record!.All(string.IsNullOrWhiteSpace),
        };

        using var streamReader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csvReader = new CsvReader(streamReader, config);

        var rows = new List<CsvRow>();
        while (csvReader.Read())
        {
            var fields = csvReader.Parser.Record!.Select(f => f.Trim()).ToList();
            rows.Add(new CsvRow(csvReader.Parser.Row, fields, csvReader.Parser.RawRecord.TrimEnd('\r', '\n')));
        }

        return rows;
    }
}
