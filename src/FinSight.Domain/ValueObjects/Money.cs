namespace FinSight.Domain.ValueObjects;

public sealed record Money
{
    /// <summary>Currency used when none is specified. FinSight is built for Canadian users.</summary>
    public const string DefaultCurrency = "CAD";

    public decimal Amount { get; }
    public string Currency { get; }

    private Money()
    {
        // Required for EF Core materialization.
        Currency = string.Empty;
    }

    public Money(decimal amount, string currency)
    {
        if (string.IsNullOrWhiteSpace(currency) || currency.Length != 3)
            throw new ArgumentException("Currency must be a 3-letter ISO code.", nameof(currency));

        Amount = Math.Round(amount, 2);
        Currency = currency.ToUpperInvariant();
    }

    public Money Add(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot add {Currency} and {other.Currency}.");
        return new Money(Amount + other.Amount, Currency);
    }

    public Money Subtract(Money other)
    {
        if (Currency != other.Currency)
            throw new InvalidOperationException($"Cannot subtract {Currency} and {other.Currency}.");
        return new Money(Amount - other.Amount, Currency);
    }

    public Money Negate() => new(-Amount, Currency);

    public bool IsPositive => Amount > 0;
    public bool IsNegative => Amount < 0;
    public bool IsZero => Amount == 0;

    public static Money Zero(string currency) => new(0, currency);
    public static Money USD(decimal amount) => new(amount, "USD");
    public static Money CAD(decimal amount) => new(amount, "CAD");

    public override string ToString() => $"{Amount:F2} {Currency}";
}
