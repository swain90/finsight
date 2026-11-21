using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace FinSight.Application.Tests.Features.Imports;

public class TransferMatchingServiceTests
{
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly Guid _userId = Guid.NewGuid();
    private static readonly DateTimeOffset Jan5 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);

    private static Transaction Tx(Account account, string desc, TransactionType type, decimal amount = 100m,
        TransactionKind kind = TransactionKind.Regular) =>
        account.ImportTransaction(desc, new Money(amount, "CAD"), type, Jan5, TransactionCategory.Uncategorized,
            kind, Guid.NewGuid(), Guid.NewGuid().ToString("N"));

    private Account Acct(string name, AccountOwner owner = AccountOwner.Personal)
    {
        var a = Account.Create(_userId, name, AccountType.Checking, "CAD", null, Institution.Other, owner, null);
        a.ClearDomainEvents();
        return a;
    }

    [Fact]
    public async Task Matching_PairsOppositeLegs_WithSharedGroup()
    {
        var debit = Tx(Acct("Chequing"), "E-TRANSFER", TransactionType.Debit);
        var credit = Tx(Acct("Savings"), "DEPOSIT", TransactionType.Credit);
        _transactions.GetMatchPoolAsync(_userId, new DateOnly(2025, 12, 31), new DateOnly(2026, 1, 10), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { debit, credit });

        await new TransferMatchingService(_transactions)
            .RunAsync(_userId, new DateOnly(2025, 12, 31).AddDays(5), new DateOnly(2026, 1, 5), default);

        debit.Kind.Should().Be(TransactionKind.Transfer);
        credit.Kind.Should().Be(TransactionKind.Transfer);
        debit.TransferGroupId.Should().NotBeNull().And.Be(credit.TransferGroupId);
    }

    [Fact]
    public async Task Matching_CorporationToPersonal_BecomesOwnerPay()
    {
        var debit = Tx(Acct("RBC Corp", AccountOwner.Corporation), "TRANSFER OUT", TransactionType.Debit);
        var credit = Tx(Acct("Personal"), "TRANSFER IN", TransactionType.Credit);
        _transactions.GetMatchPoolAsync(_userId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { debit, credit });

        await new TransferMatchingService(_transactions).RunAsync(_userId, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5), default);

        debit.Kind.Should().Be(TransactionKind.OwnerPay);
        credit.Kind.Should().Be(TransactionKind.OwnerPay);
        credit.Category.Should().Be(TransactionCategory.PayFromCorporation);
    }

    [Fact]
    public async Task Matching_HintedUnpairedRow_BecomesPending_AndStaleRowReturnsToRegularWithoutDismissal()
    {
        var hinted = Tx(Acct("A"), "PAYMENT THANK YOU", TransactionType.Credit, 50m);
        var stale = Tx(Acct("B"), "COFFEE", TransactionType.Debit, 7m, TransactionKind.PendingTransfer);
        _transactions.GetMatchPoolAsync(_userId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { hinted, stale });

        await new TransferMatchingService(_transactions).RunAsync(_userId, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5), default);

        hinted.Kind.Should().Be(TransactionKind.PendingTransfer);
        stale.Kind.Should().Be(TransactionKind.Regular);
        stale.IsTransferDismissed.Should().BeFalse();
    }

    [Fact]
    public async Task Matching_DismissedRowsUntouched()
    {
        var dismissed = Tx(Acct("Chequing"), "E-TRANSFER", TransactionType.Debit, 100m, TransactionKind.PendingTransfer);
        dismissed.MarkNotTransfer();
        var other = Tx(Acct("Savings"), "DEPOSIT", TransactionType.Credit);
        _transactions.GetMatchPoolAsync(_userId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { dismissed, other });

        await new TransferMatchingService(_transactions).RunAsync(_userId, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5), default);

        dismissed.Kind.Should().Be(TransactionKind.Regular);
        dismissed.TransferGroupId.Should().BeNull();
        dismissed.IsTransferDismissed.Should().BeTrue();
        other.Kind.Should().Be(TransactionKind.Regular);
        other.TransferGroupId.Should().BeNull();
    }
}
