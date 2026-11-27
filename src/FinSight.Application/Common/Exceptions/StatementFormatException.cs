namespace FinSight.Application.Common.Exceptions;

/// <summary>Thrown by a statement reader when a row cannot be interpreted.</summary>
public sealed class StatementFormatException(int line, string message) : Exception(message)
{
    public int Line { get; } = line;

    /// <summary>1-based PDF page the problem was found on; null for CSV rows.</summary>
    public int? Page { get; private init; }

    public StatementFormatException(int line, int page, string message) : this(line, message) => Page = page;
}
