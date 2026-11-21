using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Reports.Queries.GetCorporationSummary;
using FinSight.Application.Features.Reports.Queries.GetNetWorth;
using FinSight.Application.Features.Reports.Queries.GetPersonalIncome;
using FinSight.Application.Tests.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinSight.Application.Tests.Features.Reports;

public sealed class ReportQueriesTests : HandlerTestBase
{
    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IBalanceSnapshotRepository _snapshots = Substitute.For<IBalanceSnapshotRepository>();
    private readonly Guid _userId = Guid.NewGuid();

    public ReportQueriesTests()
    {
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(_userId);
        _snapshots.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<BalanceSnapshot>());
    }

    private static Account Acct(Guid userId, AccountType type, AccountOwner owner) =>
        Account.Create(userId, "A", type, "CAD", "Bank", Institution.Other, owner);

    private static Transaction Imp(Account a, TransactionType type, decimal amt, DateTime date,
        TransactionKind kind = TransactionKind.Regular, TransactionCategory cat = TransactionCategory.Other)
    {
        var paired = kind is TransactionKind.Transfer or TransactionKind.OwnerPay;
        var t = a.ImportTransaction("x", new(amt, "CAD"), type,
            new DateTimeOffset(date, TimeSpan.Zero), cat, paired ? TransactionKind.PendingTransfer : kind,
            Guid.NewGuid(), Guid.NewGuid().ToString("N"));
        if (paired)
            t.MarkPaired(Guid.NewGuid(), kind, kind == TransactionKind.OwnerPay ? cat : null);
        return t;
    }

    [Fact]
    public async Task CorporationSummary_ExcludesPersonalToCorpTransfer()
    {
        var user = CreateUser();
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, _userId);
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);

        var corp = Acct(_userId, AccountType.Checking, AccountOwner.Corporation);
        var personal = Acct(_userId, AccountType.Checking, AccountOwner.Personal);
        corp.UpdateBalance(new(7000m, "CAD"));
        var revenue = Imp(corp, TransactionType.Credit, 10000m, new(2026, 3, 10));
        var capital = Imp(corp, TransactionType.Credit, 500m, new(2026, 3, 11), TransactionKind.Transfer);
        var pay = Imp(corp, TransactionType.Debit, 3000m, new(2026, 3, 15), TransactionKind.OwnerPay);
        var expense = Imp(corp, TransactionType.Debit, 200m, new(2026, 4, 2));
        var persSpend = Imp(personal, TransactionType.Debit, 99m, new(2026, 3, 5));
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<Account> { corp, personal });
        _transactions.GetByUserIdAsync(_userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { revenue, capital, pay, expense, persSpend });

        var sut = new GetCorporationSummaryQueryHandler(_transactions, _accounts, _snapshots, _users, _currentUser,
            new FixedTime(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));
        var result = await sut.Handle(new GetCorporationSummaryQuery(null), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var dto = result.Value;
        dto.FiscalYearStart.Should().Be(new DateOnly(2026, 1, 1));
        dto.FiscalYearEnd.Should().Be(new DateOnly(2026, 12, 31));
        dto.Revenue.Should().Be(10000m);
        dto.PaidToOwner.Should().Be(3000m);
        dto.Expenses.Should().Be(200m);
        dto.ClosingBalance.Should().Be(7000m);
        dto.Months.Should().HaveCount(12);
        dto.Months.Single(m => m.Month == 3).Revenue.Should().Be(10000m);
    }

    [Fact]
    public async Task PersonalIncome_SplitsPayOtherSheltered()
    {
        var personal = Acct(_userId, AccountType.Checking, AccountOwner.Personal);
        var tfsa = Acct(_userId, AccountType.Tfsa, AccountOwner.Personal);
        var pay = Imp(personal, TransactionType.Credit, 6000m, new(2026, 2, 1), TransactionKind.OwnerPay, TransactionCategory.PayFromCorporation);
        var other = Imp(personal, TransactionType.Credit, 1000m, new(2026, 5, 1), TransactionKind.Regular, TransactionCategory.Salary);
        var sheltered = Imp(tfsa, TransactionType.Credit, 80m, new(2026, 6, 1), TransactionKind.Regular, TransactionCategory.InvestmentIncome);
        var spend = Imp(personal, TransactionType.Debit, 50m, new(2026, 6, 2));
        _transactions.GetByUserIdAsync(_userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { pay, other, sheltered, spend });

        var sut = new GetPersonalIncomeQueryHandler(_transactions, _currentUser,
            new FixedTime(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));
        var result = await sut.Handle(new GetPersonalIncomeQuery(2026), CancellationToken.None);

        result.Value.Year.Should().Be(2026);
        result.Value.PayFromCorporation.Should().Be(6000m);
        result.Value.OtherTaxableIncome.Should().Be(1000m);
        result.Value.TaxShelteredInvestmentIncome.Should().Be(80m);
    }

    [Fact]
    public async Task CorporationSummary_ClosingBalance_IncludesInactiveCorporationAccount()
    {
        var user = CreateUser();
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(user);
        var active = Acct(_userId, AccountType.Checking, AccountOwner.Corporation);
        var closed = Acct(_userId, AccountType.Savings, AccountOwner.Corporation);
        active.UpdateBalance(new(1000m, "CAD"));
        closed.UpdateBalance(new(250m, "CAD"));
        closed.Deactivate();
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<Account> { active, closed });
        _transactions.GetByUserIdAsync(_userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());

        var sut = new GetCorporationSummaryQueryHandler(_transactions, _accounts, _snapshots, _users, _currentUser,
            new FixedTime(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));
        var result = await sut.Handle(new GetCorporationSummaryQuery(null), CancellationToken.None);

        result.Value.ClosingBalance.Should().Be(1250m);
    }

    [Fact]
    public async Task CorporationSummary_ClosingBalance_SubtractsCorporateLiabilities()
    {
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        var chequing = Acct(_userId, AccountType.Checking, AccountOwner.Corporation);
        var card = Acct(_userId, AccountType.CreditCard, AccountOwner.Corporation);
        var personal = Acct(_userId, AccountType.Checking, AccountOwner.Personal);
        chequing.UpdateBalance(new(7000m, "CAD"));
        card.UpdateBalance(new(300m, "CAD")); // amount owed
        personal.UpdateBalance(new(50m, "CAD"));
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<Account> { chequing, card, personal });
        _transactions.GetByUserIdAsync(_userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());

        var sut = new GetCorporationSummaryQueryHandler(_transactions, _accounts, _snapshots, _users, _currentUser,
            new FixedTime(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));
        var result = await sut.Handle(new GetCorporationSummaryQuery(null), CancellationToken.None);

        result.Value.ClosingBalance.Should().Be(6700m);
    }

    [Fact]
    public async Task CorporationSummary_PastFiscalYear_UsesSnapshotsAtFiscalYearEnd()
    {
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        var chequing = Acct(_userId, AccountType.Checking, AccountOwner.Corporation);
        var card = Acct(_userId, AccountType.CreditCard, AccountOwner.Corporation);
        var openedLater = Acct(_userId, AccountType.Savings, AccountOwner.Corporation);
        chequing.UpdateBalance(new(9000m, "CAD"));
        card.UpdateBalance(new(100m, "CAD"));
        openedLater.UpdateBalance(new(777m, "CAD"));
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<Account> { chequing, card, openedLater });
        BalanceSnapshot Snap(Account a, DateOnly d, decimal v) => BalanceSnapshot.Create(a.Id, d, new(v, "CAD"), null);
        _snapshots.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<BalanceSnapshot>
        {
            Snap(chequing, new(2025, 11, 30), 4000m),
            Snap(chequing, new(2025, 12, 31), 5000m),
            Snap(chequing, new(2026, 6, 30), 9000m),
            Snap(card, new(2025, 11, 30), 400m),
            Snap(card, new(2026, 7, 31), 100m),
            Snap(openedLater, new(2026, 3, 31), 777m),
        });
        _transactions.GetByUserIdAsync(_userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());

        var sut = new GetCorporationSummaryQueryHandler(_transactions, _accounts, _snapshots, _users, _currentUser,
            new FixedTime(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));
        var result = await sut.Handle(new GetCorporationSummaryQuery(2025), CancellationToken.None);

        // Dec 31 2025: chequing 5000, card owes 400 (Nov 30); the savings account had no balance yet.
        result.Value.ClosingBalance.Should().Be(4600m);
    }

    [Fact]
    public async Task CorporationSummary_CurrentFiscalYear_PrefersSnapshotOverRunningBalance()
    {
        _users.GetByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(CreateUser());
        var chequing = Acct(_userId, AccountType.Checking, AccountOwner.Corporation);
        var cash = Acct(_userId, AccountType.Savings, AccountOwner.Corporation);
        chequing.UpdateBalance(new(8100m, "CAD")); // e.g. a manual entry after the last snapshot
        cash.UpdateBalance(new(20m, "CAD"));       // never imported: falls back to Balance
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<Account> { chequing, cash });
        _snapshots.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<BalanceSnapshot>
        {
            BalanceSnapshot.Create(chequing.Id, new(2026, 7, 31), new(8000m, "CAD"), null),
        });
        _transactions.GetByUserIdAsync(_userId, Arg.Any<DateTimeOffset>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction>());

        var sut = new GetCorporationSummaryQueryHandler(_transactions, _accounts, _snapshots, _users, _currentUser,
            new FixedTime(new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero)));
        var result = await sut.Handle(new GetCorporationSummaryQuery(null), CancellationToken.None);

        result.Value.ClosingBalance.Should().Be(8020m);
    }

    [Fact]
    public async Task NetWorth_IncludesInactiveAccountInCurrentAndHistory()
    {
        var snapshots = Substitute.For<IBalanceSnapshotRepository>();
        var active = Acct(_userId, AccountType.Savings, AccountOwner.Personal);
        var closed = Acct(_userId, AccountType.Tfsa, AccountOwner.Personal);
        active.UpdateBalance(new(5000m, "CAD"));
        closed.UpdateBalance(new(20000m, "CAD"));
        closed.Deactivate();
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<Account> { active, closed });
        BalanceSnapshot Snap(Account a, DateOnly d, decimal v) => BalanceSnapshot.Create(a.Id, d, new(v, "CAD"), null);
        snapshots.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<BalanceSnapshot>
        {
            Snap(active, new(2026, 5, 31), 4000m),
            Snap(active, new(2026, 7, 31), 4800m),
            Snap(closed, new(2026, 5, 31), 20000m),
            Snap(closed, new(2026, 6, 30), 20000m),
            Snap(closed, new(2026, 7, 15), 0m),
        });

        // Today = Aug 10 2026: window of 5 months = Apr..Aug month-ends.
        var sut = new GetNetWorthQueryHandler(_accounts, snapshots, _currentUser,
            new FixedTime(new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero)));
        var result = await sut.Handle(new GetNetWorthQuery(5), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Personal.Should().Be(25000m);
        result.Value.IncludingCorporation.Should().Be(25000m);
        // Apr 30 has no snapshots anywhere: skipped. Window ends at Aug 31 (current month end).
        result.Value.History.Select(p => (p.MonthEnd, p.Personal)).Should().Equal(
            (new DateOnly(2026, 5, 31), 24000m),
            (new DateOnly(2026, 6, 30), 24000m),
            (new DateOnly(2026, 7, 31), 4800m),
            (new DateOnly(2026, 8, 31), 4800m));
    }
}
