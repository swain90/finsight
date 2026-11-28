namespace FinSight.Domain.Exceptions;

/// <summary>
/// A save failed because it violated a unique index. Raised by the persistence layer (which owns the
/// provider-specific details) so callers can handle races without referencing Infrastructure.
/// </summary>
public sealed class UniqueViolationException(string message, Exception inner) : Exception(message, inner);
