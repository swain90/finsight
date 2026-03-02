namespace FinSight.Domain.ValueObjects;

public sealed record Address(
    string Street,
    string City,
    string Province,
    string PostalCode,
    string Country
);
