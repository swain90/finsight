using Bogus;

namespace FinSight.Api.IntegrationTests.Helpers;

/// <summary>Bogus-based builders for realistic test request payloads.</summary>
public static class TestDataBuilder
{
    private static readonly Faker Faker = new("en_CA");

    public static object RegisterRequest(string? email = null) => new
    {
        email = email ?? Faker.Internet.Email(),
        password = "Test@Password1!",
        firstName = Faker.Name.FirstName(),
        lastName = Faker.Name.LastName()
    };

    public static object CreateAccountRequest(
        string name = "TD Chequing",
        int type = 1,   // AccountType.Checking
        string currency = "CAD") => new
    {
        name,
        type,
        currency,
        institutionName = Faker.Company.CompanyName()
    };

    public static object RecordTransactionRequest(Guid accountId, bool isCredit = false) => new
    {
        accountId,
        description = Faker.Commerce.ProductName(),
        amount = Faker.Finance.Amount(5, 500),
        currency = "CAD",
        type = isCredit ? 2 : 1,   // Credit = 2, Debit = 1
        occurredAt = DateTimeOffset.UtcNow.AddDays(-Faker.Random.Int(0, 30)),
        category = 1,  // Food
        merchantName = Faker.Company.CompanyName()
    };

    public static object CreateBudgetRequest(int category = 1, decimal limit = 500m) => new
    {
        category,
        limitAmount = limit,
        currency = "CAD",
        month = DateTime.UtcNow.Month,
        year = DateTime.UtcNow.Year
    };
}
