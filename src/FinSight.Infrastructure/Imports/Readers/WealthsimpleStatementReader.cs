using System.Globalization;
using FinSight.Application.Common.Exceptions;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;

namespace FinSight.Infrastructure.Imports.Readers;

/// <summary>
/// Wealthsimple account CSV (cash and TFSA): "date","transaction","description","amount","balance","currency".
/// The sign of <c>amount</c> sets the direction; zero-amount rows (securities lending events, reorganizations)
/// carry no cash movement and are skipped.
/// </summary>
public sealed class WealthsimpleStatementReader : IStatementReader
{
    private static readonly string[] ExpectedHeader = ["date", "transaction", "description", "amount", "balance", "currency"];

    private const int FieldCount = 6;

    public Institution Institution => Institution.Wealthsimple;

    public bool CanRead(IReadOnlyList<CsvRow> firstRows) =>
        firstRows.Count > 0
        && firstRows[0].Fields.Count == FieldCount
        && firstRows[0].Fields.Zip(ExpectedHeader).All(p => string.Equals(p.First, p.Second, StringComparison.OrdinalIgnoreCase));

    public IEnumerable<ParsedStatementRow> Read(IReadOnlyList<CsvRow> allRows)
    {
        foreach (var row in allRows.Skip(1))
        {
            if (row.Fields.Count != FieldCount)
                throw new StatementFormatException(row.LineNumber, $"expected {FieldCount} fields but found {row.Fields.Count}.");

            if (!DateOnly.TryParseExact(row.Fields[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                throw new StatementFormatException(row.LineNumber, $"invalid date '{row.Fields[0]}'.");

            if (!decimal.TryParse(row.Fields[3], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var amount))
                throw new StatementFormatException(row.LineNumber, $"invalid amount '{row.Fields[3]}'.");

            var code = row.Fields[1];
            var (kind, category, isCashCode) = Classify(row.LineNumber, code);

            if (amount == 0)
                continue;

            if (!isCashCode)
                throw new StatementFormatException(row.LineNumber, $"Wealthsimple transaction code '{code}' must have a zero amount.");

            yield return new ParsedStatementRow(
                row.LineNumber,
                date,
                row.Fields[2],
                MerchantName: null,
                Math.Abs(amount),
                amount < 0 ? TransactionType.Debit : TransactionType.Credit,
                row.Fields[5],
                ExternalAccountRef: null,
                kind,
                category,
                row.RawLine);
        }
    }

    private static (TransactionKind Kind, TransactionCategory? Category, bool IsCashCode) Classify(int line, string code) =>
        code switch
        {
            "INT" or "FPLINT" or "DIV" or "NRT" => (TransactionKind.Regular, TransactionCategory.InvestmentIncome, true),
            "BUY" => (TransactionKind.InvestmentTrade, null, true),
            "AFT_IN" or "E_TRFIN" or "E_TRFOUT" or "OBP_OUT" => (TransactionKind.PendingTransfer, null, true),
            "AFT_OUT" => (TransactionKind.Regular, null, true),
            "LOAN" or "RECALL" or "STKREORG" => (TransactionKind.Regular, null, false),
            _ => throw new StatementFormatException(line, $"Unknown Wealthsimple transaction code '{code}'."),
        };
}
