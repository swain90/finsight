// =============================================================================
// DomainExceptions.cs — Domain-specific exception types
// =============================================================================
//
// The domain layer defines its own exception hierarchy.
// These exceptions are caught by the Application layer's exception handling
// middleware and converted to appropriate HTTP responses (400, 404, 409, etc.)
//
// WHY CUSTOM EXCEPTIONS?
//   Using generic Exception or ArgumentException everywhere loses semantic meaning.
//   "DomainException: Cannot add transaction with zero amount" is far more
//   diagnostic than "ArgumentException: Value cannot be zero."
//
//   Exception types also allow the global error handler to map them correctly:
//     DomainException        → 400 Bad Request
//     NotFoundException      → 404 Not Found
//     ConflictException      → 409 Conflict
//     UnauthorizedException  → 403 Forbidden
// =============================================================================

namespace FinSight.Domain.Exceptions;

/// <summary>
/// Base exception for all domain rule violations.
/// Thrown when a business invariant is broken (e.g., zero-amount transaction).
/// Maps to HTTP 400 Bad Request.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Creates a domain exception with the violation message.</summary>
    public DomainException(string message) : base(message) { }

    /// <summary>Creates a domain exception wrapping an inner exception.</summary>
    public DomainException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown when a requested domain object does not exist.
/// Maps to HTTP 404 Not Found.
/// </summary>
public class NotFoundException : DomainException
{
    /// <summary>
    /// Creates a NotFoundException for a specific entity.
    /// </summary>
    /// <param name="entityName">The type of entity that wasn't found (e.g., "Transaction").</param>
    /// <param name="key">The key that was searched for (e.g., the ID as a string).</param>
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with identifier '{key}' was not found.") { }
}

/// <summary>
/// Thrown when a user attempts an operation they are not authorized to perform.
/// Used for ownership violations (BOLA attacks).
/// Maps to HTTP 403 Forbidden.
/// </summary>
public class UnauthorizedDomainException : DomainException
{
    /// <summary>Creates an authorization exception.</summary>
    public UnauthorizedDomainException(string message = "You do not have permission to perform this action.")
        : base(message) { }
}

/// <summary>
/// Thrown when an operation would create a duplicate of a unique resource.
/// Maps to HTTP 409 Conflict.
/// </summary>
public class ConflictException : DomainException
{
    /// <summary>Creates a conflict exception.</summary>
    public ConflictException(string message) : base(message) { }
}
