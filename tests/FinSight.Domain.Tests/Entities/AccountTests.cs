using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace FinSight.Domain.Tests.Entities;

public class AccountTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateAccount()
    {
        // Arrange & Act
        var account = Account.Create(Guid.NewGuid(), "Chequing Account", AccountType.Checking, "CAD", "TD Bank");

        // Assert
        account.Name.Should().Be("Chequing Account");
        account.Type.Should().Be(AccountType.Checking);
        account.Balance.Amount.Should().Be(0);
        account.Balance.Currency.Should().Be("CAD");
        account.InstitutionName.Should().Be("TD Bank");
        account.IsActive.Should().BeTrue();
        account.DomainEvents.Should().HaveCount(1);
    }

    [Fact]
    public void RecordTransaction_Credit_ShouldIncreaseBalance()
    {
        // Arrange
        var account = Account.Create(Guid.NewGuid(), "Savings", AccountType.Savings, "USD");
        var amount = new Money(500m, "USD");

        // Act
        account.RecordTransaction("Salary deposit", amount, TransactionType.Credit,
            DateTimeOffset.UtcNow, TransactionCategory.Salary);

        // Assert
        account.Balance.Amount.Should().Be(500m);
        account.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public void RecordTransaction_Debit_ShouldDecreaseBalance()
    {
        // Arrange
        var account = Account.Create(Guid.NewGuid(), "Checking", AccountType.Checking, "USD");
        account.RecordTransaction("Salary", Money.USD(1000), TransactionType.Credit, DateTimeOffset.UtcNow);
        var initialBalance = account.Balance.Amount;

        // Act
        account.RecordTransaction("Groceries", Money.USD(150), TransactionType.Debit, DateTimeOffset.UtcNow,
            TransactionCategory.GroceriesAndFood, "Walmart");

        // Assert
        account.Balance.Amount.Should().Be(initialBalance - 150);
    }

    [Fact]
    public void RecordTransaction_OnInactiveAccount_ShouldThrowDomainException()
    {
        // Arrange
        var account = Account.Create(Guid.NewGuid(), "Closed", AccountType.Checking);
        account.Deactivate();

        // Act & Assert
        var act = () => account.RecordTransaction("test", Money.USD(100), TransactionType.Debit, DateTimeOffset.UtcNow);
        act.Should().Throw<DomainException>().WithMessage("*inactive*");
    }

    [Fact]
    public void Create_WithoutCurrency_DefaultsToCad()
    {
        var account = Account.Create(Guid.NewGuid(), "Chequing", AccountType.Checking);

        account.Balance.Currency.Should().Be("CAD");
        Money.DefaultCurrency.Should().Be("CAD");
    }
}
