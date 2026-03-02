using Bogus;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;

namespace FinSight.Application.Tests.Common;

public abstract class HandlerTestBase
{
    protected static readonly Faker Faker = new("en_CA");

    protected static User CreateUser(string? email = null, string? passwordHash = null)
    {
        var user = User.Create(
            email ?? Faker.Internet.Email(),
            passwordHash ?? "$2a$12$fakehashAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            Faker.Name.FirstName(),
            Faker.Name.LastName());
        user.ClearDomainEvents();
        return user;
    }

    protected static Account CreateAccount(Guid? userId = null)
    {
        var account = Account.Create(
            userId ?? Guid.NewGuid(),
            Faker.Finance.AccountName(),
            AccountType.Checking,
            "CAD",
            Faker.Company.CompanyName());
        account.ClearDomainEvents();
        return account;
    }

    protected static Money CAD(decimal amount) => new(amount, "CAD");
}
