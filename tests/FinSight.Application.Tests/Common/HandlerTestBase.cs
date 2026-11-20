using Bogus;
using FinSight.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
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

    /// <summary>Db context substitute whose DbSets accept Add/AddAsync without a real database.</summary>
    internal static IApplicationDbContext CreateDbContext()
    {
        var db = Substitute.For<IApplicationDbContext>();
        db.Users.Returns(Substitute.For<DbSet<User>>());
        db.Accounts.Returns(Substitute.For<DbSet<Account>>());
        db.Transactions.Returns(Substitute.For<DbSet<Transaction>>());
        db.Budgets.Returns(Substitute.For<DbSet<Budget>>());
        db.RefreshTokens.Returns(Substitute.For<DbSet<RefreshToken>>());
        return db;
    }

    protected static Money CAD(decimal amount) => new(amount, "CAD");
}
