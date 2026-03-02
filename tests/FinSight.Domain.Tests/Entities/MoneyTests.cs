// =============================================================================
// MoneyTests.cs — Unit Tests for the Money Value Object
// =============================================================================
//
// These tests verify the core financial arithmetic and validation rules
// of the Money value object. They are the most critical tests in the system
// — money calculation bugs can have severe real-world consequences.
//
// TEST NAMING CONVENTION: MethodName_StateUnderTest_ExpectedBehavior
// Example: Add_SameCurrency_ReturnsSumAmount
//
// WHY TEST THE DOMAIN LAYER FIRST?
//   Domain tests are the fastest, most reliable tests in the system.
//   They have zero dependencies (no DB, no HTTP, no mocks needed).
//   A comprehensive domain test suite catches 80% of bugs with minimal complexity.
//
// TOOLS USED:
//   xUnit  — test framework (preferred over NUnit for .NET Core)
//   FluentAssertions — readable assertion syntax
//   No mocking needed — domain objects have no external dependencies
// =============================================================================

using FinSight.Domain.ValueObjects;
using FluentAssertions;

namespace FinSight.Domain.Tests.Entities;

/// <summary>
/// Tests for the <see cref="Money"/> value object.
/// Verifies: construction, arithmetic, comparisons, and edge cases.
/// </summary>
public sealed class MoneyTests
{
    // =========================================================================
    // CONSTRUCTION
    // =========================================================================

    [Fact]
    public void Of_ValidAmountAndCurrency_CreatesMoneyInstance()
    {
        // Arrange & Act
        var money = Money.Of(100.50m, "USD");

        // Assert
        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("USD");
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("CAD")]
    [InlineData("JPY")]
    public void Of_ValidIsoCurrencyCode_AcceptsCode(string currency)
    {
        // All valid ISO 4217 3-letter codes should be accepted
        var action = () => Money.Of(100m, currency);
        action.Should().NotThrow();
    }

    [Theory]
    [InlineData("US")]        // Too short
    [InlineData("USDT")]      // Too long (crypto, not ISO 4217)
    [InlineData("123")]       // Not letters
    [InlineData("")]          // Empty
    [InlineData("  ")]        // Whitespace
    public void Of_InvalidCurrencyCode_ThrowsArgumentException(string invalidCurrency)
    {
        // Invalid currency codes must be rejected at construction time
        var action = () => Money.Of(100m, invalidCurrency);
        action.Should().Throw<ArgumentException>()
            .WithMessage("*ISO 4217*");
    }

    [Fact]
    public void Of_CurrencyStoredAsUpperCase_RegardlessOfInput()
    {
        // Currency should always be normalized to uppercase
        var money = Money.Of(100m, "usd");
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Zero_ReturnsMoneywithZeroAmount()
    {
        var zero = Money.Zero("USD");
        zero.Amount.Should().Be(0m);
        zero.Currency.Should().Be("USD");
        zero.IsZero.Should().BeTrue();
    }

    // =========================================================================
    // ARITHMETIC OPERATIONS
    // =========================================================================

    [Fact]
    public void Add_SameCurrency_ReturnsSumAmount()
    {
        // Arrange
        var price = Money.Of(100m, "USD");
        var tax   = Money.Of(8.75m, "USD");

        // Act
        var total = price + tax;

        // Assert
        total.Amount.Should().Be(108.75m);
        total.Currency.Should().Be("USD");
    }

    [Fact]
    public void Add_DifferentCurrencies_ThrowsInvalidOperationException()
    {
        // CRITICAL: Adding different currencies must ALWAYS throw
        // This prevents silent currency mixing bugs that can cause financial loss
        var dollars = Money.Of(100m, "USD");
        var euros   = Money.Of(100m, "EUR");

        var action = () => dollars + euros;

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*USD*EUR*");
    }

    [Fact]
    public void Subtract_SameCurrency_ReturnsDifference()
    {
        var balance  = Money.Of(1000m, "USD");
        var purchase = Money.Of(250m, "USD");

        var remaining = balance - purchase;

        remaining.Amount.Should().Be(750m);
    }

    [Fact]
    public void Subtract_ResultCanBeNegative_AllowsOverdraft()
    {
        // Negative balances (overdrafts) are valid in the domain
        // Whether to ALLOW overdraft is a business rule, not a Money rule
        var balance  = Money.Of(50m, "USD");
        var purchase = Money.Of(100m, "USD");

        var result = balance - purchase;

        result.Amount.Should().Be(-50m);
        result.IsNegative.Should().BeTrue();
    }

    [Theory]
    [InlineData(100.00, 0.0875, 8.75)]   // 8.75% tax on $100
    [InlineData(199.99, 0.10, 19.999)]    // 10% of $199.99
    [InlineData(0.00, 0.10, 0.00)]        // 10% of nothing = nothing
    public void Multiply_ByFactor_ReturnsScaledAmount(
        decimal amount, decimal factor, decimal expected)
    {
        var money  = Money.Of(amount, "USD");
        var result = money * factor;

        result.Amount.Should().BeApproximately(expected, precision: 0.0001m);
        result.Currency.Should().Be("USD");
    }

    [Fact]
    public void Negate_PositiveMoney_ReturnsNegativeAmount()
    {
        // Useful for reversals/refunds
        var money    = Money.Of(100m, "USD");
        var negated  = -money;

        negated.Amount.Should().Be(-100m);
        negated.IsNegative.Should().BeTrue();
    }

    // =========================================================================
    // COMPARISON OPERATORS
    // =========================================================================

    [Fact]
    public void GreaterThan_LargerAmount_ReturnsTrue()
    {
        var big   = Money.Of(200m, "USD");
        var small = Money.Of(100m, "USD");

        (big > small).Should().BeTrue();
        (small > big).Should().BeFalse();
    }

    [Fact]
    public void Comparison_DifferentCurrencies_ThrowsInvalidOperationException()
    {
        var dollars = Money.Of(100m, "USD");
        var euros   = Money.Of(100m, "EUR");

        var action = () => _ = dollars > euros;

        action.Should().Throw<InvalidOperationException>();
    }

    // =========================================================================
    // QUERY PROPERTIES
    // =========================================================================

    [Fact]
    public void IsZero_ZeroAmount_ReturnsTrue()
        => Money.Zero("USD").IsZero.Should().BeTrue();

    [Fact]
    public void IsZero_NonZeroAmount_ReturnsFalse()
        => Money.Of(0.01m, "USD").IsZero.Should().BeFalse();

    [Fact]
    public void IsPositive_PositiveAmount_ReturnsTrue()
        => Money.Of(1m, "USD").IsPositive.Should().BeTrue();

    [Fact]
    public void IsNegative_NegativeAmount_ReturnsTrue()
        => Money.Of(-1m, "USD").IsNegative.Should().BeTrue();

    [Fact]
    public void Abs_NegativeAmount_ReturnsPositive()
    {
        var negative = Money.Of(-100m, "USD");
        negative.Abs().Amount.Should().Be(100m);
    }

    // =========================================================================
    // VALUE EQUALITY (record semantics)
    // =========================================================================

    [Fact]
    public void TwoMoneyInstances_SameAmountAndCurrency_AreEqual()
    {
        // Records provide structural equality by default
        var money1 = Money.Of(100m, "USD");
        var money2 = Money.Of(100m, "USD");

        money1.Should().Be(money2);
        (money1 == money2).Should().BeTrue();
    }

    [Fact]
    public void TwoMoneyInstances_DifferentAmount_AreNotEqual()
    {
        var money1 = Money.Of(100m, "USD");
        var money2 = Money.Of(200m, "USD");

        money1.Should().NotBe(money2);
    }

    [Fact]
    public void TwoMoneyInstances_DifferentCurrency_AreNotEqual()
    {
        var dollars = Money.Of(100m, "USD");
        var euros   = Money.Of(100m, "EUR");

        dollars.Should().NotBe(euros);
    }

    // =========================================================================
    // PRECISION — critical for financial calculations
    // =========================================================================

    [Fact]
    public void DecimalPrecision_SmallFractionalAmounts_ExactRepresentation()
    {
        // This test would FAIL if we used double/float instead of decimal
        // Binary floating-point cannot exactly represent 0.1
        var money = Money.Of(0.1m, "USD");
        money.Amount.Should().Be(0.1m);  // Exactly 0.1, not 0.10000000000000001
    }

    [Fact]
    public void Addition_RepeatedSmallAmounts_ExactTotal()
    {
        // Classic floating-point precision test: add 0.1 ten times
        // With double/float: 0.1 + 0.1 + ... (10 times) ≠ 1.0
        // With decimal: exactly 1.0
        var penny  = Money.Of(0.1m, "USD");
        var total  = Money.Zero("USD");

        for (var i = 0; i < 10; i++)
        {
            total += penny;
        }

        total.Amount.Should().Be(1.0m);  // Exactly 1.0
    }
}
