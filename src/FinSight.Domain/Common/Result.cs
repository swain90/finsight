namespace FinSight.Domain.Common;

public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None) throw new InvalidOperationException();
        if (!isSuccess && error == Error.None) throw new InvalidOperationException();
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);
    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);
}

public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error) : base(isSuccess, error)
        => _value = value;

    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("Cannot access value of a failed result.");
}

public sealed record Error(string Code, string Description)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NotFound = new("NotFound", "Resource not found.");
    public static readonly Error Unauthorized = new("Unauthorized", "Unauthorized access.");
    public static readonly Error Conflict = new("Conflict", "A conflict occurred.");
    public static readonly Error Validation = new("Validation", "Validation failed.");

    public static Error Custom(string code, string description) => new(code, description);
}
