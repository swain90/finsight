namespace FinSight.Application.Common.Exceptions;

/// <summary>Thrown by a statement reader when a row cannot be interpreted.</summary>
public sealed class StatementFormatException(int line, string message) : Exception(message)
{
    public int Line { get; } = line;
}
