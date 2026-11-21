using System.Globalization;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Imports;

namespace FinSight.Infrastructure.Tests.Imports;

/// <summary>
/// Test-only reader for files shaped "Date,Description,Amount[,Currency]" (signed amount, ISO dates).
/// With <paramref name="passAmountThrough"/>, the parsed amount is emitted unchanged (as a Debit), to simulate a
/// faulty reader that breaks the "amounts are always positive" contract.
/// </summary>
internal sealed class FakeReader(
    bool canRead = true, Institution institution = Institution.Cibc, bool passAmountThrough = false) : IStatementReader
{
    public Institution Institution => institution;

    public bool CanRead(IReadOnlyList<CsvRow> firstRows) =>
        canRead && firstRows.Count > 0 && firstRows[0].Fields.SequenceEqual(new[] { "Date", "Description", "Amount" });

    public IEnumerable<ParsedStatementRow> Read(IReadOnlyList<CsvRow> allRows)
    {
        foreach (var row in allRows.Skip(1))
        {
            if (!decimal.TryParse(row.Fields[2], NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                throw new StatementFormatException(row.LineNumber, "invalid amount.");
            var currency = row.Fields.Count > 3 ? row.Fields[3] : "CAD";
            yield return new ParsedStatementRow(
                row.LineNumber, DateOnly.Parse(row.Fields[0], CultureInfo.InvariantCulture), row.Fields[1], null,
                passAmountThrough ? amount : Math.Abs(amount),
                passAmountThrough || amount < 0 ? TransactionType.Debit : TransactionType.Credit, currency, null,
                TransactionKind.Regular, null, row.RawLine);
        }
    }
}
