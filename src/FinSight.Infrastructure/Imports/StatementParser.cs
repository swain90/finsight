using System.Globalization;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.ValueObjects;
using FinSight.Infrastructure.Imports.Pdf;
using Microsoft.Extensions.Logging;

namespace FinSight.Infrastructure.Imports;

public sealed partial class StatementParser(
    IEnumerable<IStatementReader> readers,
    IEnumerable<IPdfStatementReader> pdfReaders,
    ILogger<StatementParser>? logger = null) : IStatementParser
{
    private const string UnknownFileMessage =
        "Unrecognized file. Expected a CSV export from CIBC, TD, MBNA, RBC or Wealthsimple, or a CIBC credit card PDF statement.";

    private const string MismatchMessage = "The file's contents don't match its .pdf/.csv extension.";

    private const string UnreadablePdfMessage =
        "The PDF could not be read. Download the statement again from CIBC and retry.";

    private const int SniffRowCount = 5;

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();

    public Result<ParsedStatement> Parse(Stream content, string fileName)
    {
        // The upload stream may be non-seekable and is capped at 5 MB by the validator.
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        var bytes = buffer.ToArray();

        var isPdf = bytes.AsSpan().StartsWith(PdfMagic);
        var extension = Path.GetExtension(fileName ?? string.Empty);
        var mismatch = (isPdf && extension.Equals(".csv", StringComparison.OrdinalIgnoreCase))
            || (!isPdf && extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase));
        if (mismatch)
            return Result.Failure<ParsedStatement>(Error.Custom("Import.FileMismatch", MismatchMessage));

        return isPdf ? ParsePdf(bytes) : ParseCsv(bytes);
    }

    private Result<ParsedStatement> ParsePdf(byte[] bytes)
    {
        PdfText text;
        try
        {
            text = PdfTextExtractor.Extract(bytes);
        }
        catch (PdfUnreadableException)
        {
            return Result.Failure<ParsedStatement>(Error.Custom("Import.PdfUnreadable", UnreadablePdfMessage));
        }

        var matches = text.Lines.Count == 0
            ? []
            : pdfReaders.Where(r => r.CanRead(text)).ToList();
        if (matches.Count != 1)
            return Result.Failure<ParsedStatement>(Error.Custom("Import.UnknownFormat", UnknownFileMessage));

        var reader = matches[0];
        ParsedStatement statement;
        try
        {
            statement = reader.Read(text);
            RequirePositive(statement.Rows);
        }
        catch (StatementFormatException ex)
        {
            var where = $"Page {ex.Page ?? PageOf(text, ex.Line)}";
            return Result.Failure<ParsedStatement>(Error.Custom("Import.Format", $"{where}: {ex.Message}"));
        }
        catch (StatementTotalsMismatchException ex)
        {
            var off = Math.Abs(ex.Difference).ToString("0.00", CultureInfo.InvariantCulture);
            return Result.Failure<ParsedStatement>(Error.Custom("Import.TotalsMismatch",
                $"This CIBC statement's transactions don't add up to its totals (off by ${off}). " +
                "The statement layout may have changed; nothing was imported."));
        }
        catch (Exception ex)
        {
            // A reader bug or an unexpected layout must not surface as a 500. Only the types are logged: the
            // message could quote statement text.
            if (logger is not null)
                LogReaderFailed(logger, reader.GetType().Name, ex.GetType().FullName ?? ex.GetType().Name);
            return Result.Failure<ParsedStatement>(Error.Custom("Import.UnknownFormat", UnknownFileMessage));
        }

        return Finish(statement.Rows, statement, r => $"Page {PageOf(text, r.LineNumber)}");
    }

    private Result<ParsedStatement> ParseCsv(byte[] bytes)
    {
        List<CsvRow> rows;
        try
        {
            rows = ReadRows(new MemoryStream(bytes));
        }
        catch (CsvHelperException ex)
        {
            var line = ex.Context?.Parser?.RawRow;
            var where = line is > 0 ? $" near row {line}" : string.Empty;
            return Result.Failure<ParsedStatement>(
                Error.Custom("Import.Malformed", $"The file could not be read as CSV{where}."));
        }

        var firstRows = rows.Take(SniffRowCount).ToList();

        var matches = readers.Where(r => r.CanRead(firstRows)).ToList();
        if (matches.Count != 1)
            return Result.Failure<ParsedStatement>(Error.Custom("Import.UnknownFormat", UnknownFileMessage));

        var reader = matches[0];
        List<ParsedStatementRow> parsed;
        try
        {
            parsed = reader.Read(rows).ToList();
            RequirePositive(parsed);
        }
        catch (StatementFormatException ex)
        {
            return Result.Failure<ParsedStatement>(
                Error.Custom("Import.Format", $"Row {ex.Line}: {ex.Message}"));
        }

        return Finish(parsed, new ParsedStatement(reader.Institution, parsed), r => $"Row {r.LineNumber}");
    }

    // Readers normalize to positive amounts with a direction; anything else is a reader bug that
    // would silently flip or zero a row, so it fails the upload like any other format error.
    private static void RequirePositive(IReadOnlyList<ParsedStatementRow> rows)
    {
        var nonPositive = rows.FirstOrDefault(r => r.Amount <= 0);
        if (nonPositive is not null)
            throw new StatementFormatException(nonPositive.LineNumber, "Amount must be positive.");
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "PDF reader {Reader} failed with {ExceptionType}")]
    private static partial void LogReaderFailed(ILogger logger, string reader, string exceptionType);

    private static int PageOf(PdfText text, int lineNumber) =>
        text.Lines.FirstOrDefault(l => l.LineNumber == lineNumber)?.Page ?? 1;

    private static Result<ParsedStatement> Finish(IReadOnlyList<ParsedStatementRow> rows, ParsedStatement statement, Func<ParsedStatementRow, string> where)
    {
        if (rows.Count == 0)
            return Result.Failure<ParsedStatement>(
                Error.Custom("Import.Empty", "The file contains no transactions."));

        var nonCad = rows.FirstOrDefault(r =>
            !string.Equals(r.Currency, Money.DefaultCurrency, StringComparison.OrdinalIgnoreCase));
        if (nonCad is not null)
            return Result.Failure<ParsedStatement>(
                Error.Custom("Import.Currency", $"{where(nonCad)}: only CAD is supported."));

        return Result.Success(statement);
    }

    private static List<CsvRow> ReadRows(Stream csv)
    {
        var config = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false,
            TrimOptions = TrimOptions.Trim,
            DetectDelimiter = false,
            MissingFieldFound = null,
            ShouldSkipRecord = args => args.Row.Parser.Record is not { } record
                || record.All(string.IsNullOrWhiteSpace),
        };

        using var streamReader = new StreamReader(csv, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        using var csvReader = new CsvReader(streamReader, config);

        var rows = new List<CsvRow>();
        while (csvReader.Read())
        {
            var fields = csvReader.Parser.Record!.Select(f => f.Trim()).ToList();
            var rawLine = csvReader.Parser.RawRecord.TrimEnd('\r', '\n');
            // Parser.RawRow is the physical line where the record ENDS (Parser.Row ignores embedded newlines); report where it starts.
            var startLine = csvReader.Parser.RawRow - rawLine.Count(c => c == '\n');
            rows.Add(new CsvRow(startLine, fields, rawLine));
        }

        return rows;
    }
}
