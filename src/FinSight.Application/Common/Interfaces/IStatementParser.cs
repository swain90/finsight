using FinSight.Domain.Common;
using FinSight.Domain.Enums;

namespace FinSight.Application.Common.Interfaces;

public interface IStatementParser
{
    /// <summary>Parses a CSV or PDF statement; the format is decided by content, <paramref name="fileName"/> only cross-checks it.</summary>
    Result<ParsedStatement> Parse(Stream content, string fileName);
}

public sealed record ParsedStatement(
    Institution Institution,
    IReadOnlyList<ParsedStatementRow> Rows,
    StatementSummary? Summary = null);

/// <summary>Statement-level figures a PDF reader can read off the page (CSV exports carry none).</summary>
public sealed record StatementSummary(
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    decimal OpeningBalance,
    decimal ClosingBalance,
    string? ExternalAccountRef);

/// <summary>
/// One parsed statement row. <paramref name="Amount"/> is always positive; money leaving, or a
/// charge on a card/line of credit, is a Debit. <paramref name="LineNumber"/> is the 1-based
/// physical line in the file (header is line 1).
/// </summary>
public sealed record ParsedStatementRow(
    int LineNumber,
    DateOnly Date,
    string Description,
    string? MerchantName,
    decimal Amount,
    TransactionType Type,
    string Currency,
    string? ExternalAccountRef,
    TransactionKind KindHint,
    TransactionCategory? CategoryHint,
    string RawLine);
