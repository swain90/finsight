using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace FinSight.Domain.Tests.Entities;

public class AccountImportTests
{
    private static Account Visa() => Account.Create(Guid.NewGuid(), "Visa", AccountType.CreditCard);

    private static Transaction Import(Account a, Money? amount = null, TransactionKind kind = TransactionKind.Regular, Guid? batch = null) =>
        a.ImportTransaction("LOBLAWS", amount ?? Money.CAD(82.15m), TransactionType.Debit, DateTimeOffset.UtcNow,
            TransactionCategory.GroceriesAndFood, kind, batch ?? Guid.NewGuid(), "fp1");

    [Fact]
    public void ImportTransaction_DoesNotChangeBalance()
    {
        var a = Visa();
        var batch = Guid.NewGuid();
        var tx = Import(a, batch: batch);
        a.Balance.Amount.Should().Be(0m);
        (tx.ImportBatchId, tx.ImportFingerprint, tx.Kind).Should().Be((batch, "fp1", TransactionKind.Regular));
    }

    [Fact]
    public void ImportTransaction_SetsAccountNavigation()
    {
        var a = Visa();
        Import(a).Account.Should().BeSameAs(a);
    }

    [Fact]
    public void RecordTransaction_SetsAccountNavigation()
    {
        var a = Visa();
        a.RecordTransaction("Coffee", Money.CAD(5m), TransactionType.Debit, DateTimeOffset.UtcNow)
            .Account.Should().BeSameAs(a);
    }

    [Fact]
    public void ImportTransaction_CurrencyMismatch_Throws()
    {
        var act = () => Import(Visa(), Money.USD(5m));
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData(TransactionKind.Transfer)]
    [InlineData(TransactionKind.OwnerPay)]
    public void ImportTransaction_PairedKind_Throws(TransactionKind kind)
    {
        var act = () => Import(Visa(), kind: kind);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void ImportTransaction_InactiveAccount_Throws()
    {
        var a = Visa();
        a.Deactivate();
        var act = () => Import(a);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void RecordTransaction_LiabilityDebit_IncreasesAmountOwed()
    {
        var a = Visa();
        a.RecordTransaction("Coffee", Money.CAD(5m), TransactionType.Debit, DateTimeOffset.UtcNow);
        a.Balance.Amount.Should().Be(5m);
    }

    [Fact]
    public void RecordTransaction_LiabilityCredit_DecreasesAmountOwed()
    {
        var a = Visa();
        a.RecordTransaction("Coffee", Money.CAD(100m), TransactionType.Debit, DateTimeOffset.UtcNow);
        a.RecordTransaction("Payment", Money.CAD(40m), TransactionType.Credit, DateTimeOffset.UtcNow);
        a.Balance.Amount.Should().Be(60m);
    }
}
