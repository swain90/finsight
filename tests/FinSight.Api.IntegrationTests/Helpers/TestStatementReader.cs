using System.Globalization;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Imports;

namespace FinSight.Api.IntegrationTests.Helpers;

/// <summary>Test-only reader for "Date,Description,Amount" files (ISO date, signed amount: negative = Debit, CAD).</summary>
public sealed class TestStatementReader : IStatementReader
{
    public Institution Institution => Institution.Other;

    public bool CanRead(IReadOnlyList<CsvRow> firstRows) =>
        firstRows.Count > 0 && firstRows[0].Fields.SequenceEqual(new[] { "Date", "Description", "Amount" });

    public IEnumerable<ParsedStatementRow> Read(IReadOnlyList<CsvRow> allRows)
    {
        foreach (var row in allRows.Skip(1))
        {
            if (!decimal.TryParse(row.Fields[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                throw new StatementFormatException(row.LineNumber, "invalid amount.");
            yield return new ParsedStatementRow(
                row.LineNumber, DateOnly.Parse(row.Fields[0], CultureInfo.InvariantCulture), row.Fields[1], null,
                Math.Abs(amount), amount < 0 ? TransactionType.Debit : TransactionType.Credit, "CAD", null,
                TransactionKind.Regular, null, row.RawLine);
        }
    }
}
