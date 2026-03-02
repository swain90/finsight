using FinSight.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace FinSight.Domain.Tests.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Constructor_WithValidData_ShouldRoundToTwoDecimals()
    {
        var money = new Money(10.555m, "USD");
        money.Amount.Should().Be(10.56m);
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_SameCurrency_ShouldReturnSum()
    {
        var a = Money.USD(100);
        var b = Money.USD(50);
        a.Add(b).Amount.Should().Be(150);
    }

    [Fact]
    public void Add_DifferentCurrencies_ShouldThrow()
    {
        var usd = Money.USD(100);
        var cad = Money.CAD(100);
        var act = () => usd.Add(cad);
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("US")]       // too short
    [InlineData("USDD")]     // too long
    [InlineData("")]         // empty
    public void Constructor_WithInvalidCurrency_ShouldThrow(string currency)
    {
        var act = () => new Money(100, currency);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Equality_SameAmountAndCurrency_ShouldBeEqual()
    {
        var a = new Money(100, "USD");
        var b = new Money(100, "USD");
        a.Should().Be(b);
    }
}
