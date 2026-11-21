using FinSight.Application.Common.Reporting;
using FinSight.Application.Tests.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinSight.Application.Tests.Common;

public sealed class PersonalLedgerTests : HandlerTestBase
{
    private static readonly DateTimeOffset When = new(2025, 6, 15, 0, 0, 0, TimeSpan.Zero);

    private static Account Acct(AccountType type = AccountType.Checking, AccountOwner owner = AccountOwner.Personal) =>
        Account.Create(Guid.NewGuid(), "A", type, "CAD", "Bank", Institution.Other, owner);

    private static Transaction Import(Account a, TransactionType type, TransactionCategory cat, TransactionKind kind, decimal amount = 50m) =>
        a.ImportTransaction("x", CAD(amount), type, When, cat, kind, Guid.NewGuid(), Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(TransactionKind.PendingTransfer)]
    [InlineData(TransactionKind.InvestmentTrade)]
    public void IsPersonalSpending_ExcludesTransferOwnerPayTradePending(TransactionKind kind)
    {
        var t = Import(Acct(), TransactionType.Debit, TransactionCategory.Other, kind);
        PersonalLedger.IsPersonalSpending(t).Should().BeFalse();
    }

    [Fact]
    public void IsPersonalSpending_ExcludesPairedTransferAndOwnerPay()
    {
        var a = Acct();
        var transfer = Import(a, TransactionType.Debit, TransactionCategory.Other, TransactionKind.PendingTransfer);
        transfer.MarkPaired(Guid.NewGuid(), TransactionKind.Transfer);
        var pay = Import(a, TransactionType.Credit, TransactionCategory.Other, TransactionKind.PendingTransfer);
        pay.MarkPaired(Guid.NewGuid(), TransactionKind.OwnerPay, TransactionCategory.PayFromCorporation);

        PersonalLedger.IsPersonalSpending(transfer).Should().BeFalse();
        PersonalLedger.IsPersonalSpending(pay).Should().BeFalse();
    }

    [Fact]
    public void IsPersonalSpending_ExcludesCorporateAccount()
    {
        var t = Import(Acct(owner: AccountOwner.Corporation), TransactionType.Debit,
            TransactionCategory.GroceriesAndFood, TransactionKind.Regular);
        PersonalLedger.IsPersonalSpending(t).Should().BeFalse();
    }

    [Fact]
    public void IsPersonalSpending_IncludesRegularDebit()
    {
        var t = Import(Acct(), TransactionType.Debit, TransactionCategory.GroceriesAndFood, TransactionKind.Regular);
        PersonalLedger.IsPersonalSpending(t).Should().BeTrue();
    }

    [Fact]
    public void IsPersonalSpending_NullAccount_Throws()
    {
        var t = Import(Acct(), TransactionType.Debit, TransactionCategory.Other, TransactionKind.Regular);
        typeof(Transaction).GetProperty(nameof(Transaction.Account))!.SetValue(t, null);

        var act = () => PersonalLedger.IsPersonalSpending(t);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Account*");
    }

    [Fact]
    public void SpendingAmount_RefundSubtracts()
    {
        var a = Acct();
        var debit = Import(a, TransactionType.Debit, TransactionCategory.Entertainment, TransactionKind.Regular, 100m);
        var refund = Import(a, TransactionType.Credit, TransactionCategory.Entertainment, TransactionKind.Regular, 30m);

        PersonalLedger.SpendingAmount(debit).Should().Be(100m);
        PersonalLedger.SpendingAmount(refund).Should().Be(-30m);
    }

    [Fact]
    public void IsTaxableIncome_OwnerPayCreditCounts()
    {
        var t = Import(Acct(), TransactionType.Credit, TransactionCategory.Other, TransactionKind.PendingTransfer, 2000m);
        t.MarkPaired(Guid.NewGuid(), TransactionKind.OwnerPay, TransactionCategory.PayFromCorporation);
        PersonalLedger.IsTaxableIncome(t).Should().BeTrue();
    }

    [Fact]
    public void IsTaxableIncome_RegularSalaryCountsButDebitDoesNot()
    {
        var a = Acct();
        PersonalLedger.IsTaxableIncome(Import(a, TransactionType.Credit, TransactionCategory.Salary, TransactionKind.Regular)).Should().BeTrue();
        PersonalLedger.IsTaxableIncome(Import(a, TransactionType.Debit, TransactionCategory.Salary, TransactionKind.Regular)).Should().BeFalse();
        PersonalLedger.IsTaxableIncome(Import(a, TransactionType.Credit, TransactionCategory.GroceriesAndFood, TransactionKind.Regular)).Should().BeFalse();
    }

    [Fact]
    public void IsTaxableIncome_TfsaDividendExcluded()
    {
        var t = Import(Acct(AccountType.Tfsa), TransactionType.Credit, TransactionCategory.InvestmentIncome, TransactionKind.Regular);
        PersonalLedger.IsTaxableIncome(t).Should().BeFalse();
    }

    [Fact]
    public void IsTaxableIncome_NonRegisteredDividendCounts()
    {
        var t = Import(Acct(AccountType.Investment), TransactionType.Credit, TransactionCategory.InvestmentIncome, TransactionKind.Regular);
        PersonalLedger.IsTaxableIncome(t).Should().BeTrue();
    }

    [Fact]
    public void IsTaxShelteredIncome_RrspInterestCounts()
    {
        var t = Import(Acct(AccountType.Rrsp), TransactionType.Credit, TransactionCategory.InvestmentIncome, TransactionKind.Regular);
        PersonalLedger.IsTaxShelteredIncome(t).Should().BeTrue();
        PersonalLedger.IsTaxableIncome(t).Should().BeFalse();
    }

    [Fact]
    public void IsTaxableIncome_CorporateAccountExcluded()
    {
        var t = Import(Acct(owner: AccountOwner.Corporation), TransactionType.Credit, TransactionCategory.Salary, TransactionKind.Regular);
        PersonalLedger.IsTaxableIncome(t).Should().BeFalse();
    }
}
