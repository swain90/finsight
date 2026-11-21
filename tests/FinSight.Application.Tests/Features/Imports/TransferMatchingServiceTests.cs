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
        // R24: candidates are computed over [from - 2W, to + 2W].
        _transactions.GetMatchPoolAsync(_userId, new DateOnly(2025, 12, 26), new DateOnly(2026, 1, 15), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { debit, credit });

        await new TransferMatchingService(_transactions)
            .RunAsync(_userId, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 5), default);

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

    private static Transaction TxOn(Account account, string desc, TransactionType type, DateOnly date,
        decimal amount = 100m, TransactionKind kind = TransactionKind.Regular) =>
        account.ImportTransaction(desc, new Money(amount, "CAD"), type,
            new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), TransactionCategory.Uncategorized,
            kind, Guid.NewGuid(), Guid.NewGuid().ToString("N"));

    /// <summary>Behaves like the real repository: returns only rows dated within the requested range.</summary>
    private void StoredRows(params Transaction[] rows) =>
        _transactions.GetMatchPoolAsync(_userId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                var from = ci.ArgAt<DateOnly>(1);
                var to = ci.ArgAt<DateOnly>(2);
                return rows.Where(r =>
                {
                    var d = DateOnly.FromDateTime(r.OccurredAt.UtcDateTime);
                    return d >= from && d <= to;
                }).ToList();
            });

    [Fact]
    public async Task Matching_EdgeRowWithCandidateJustOutsideRange_StaysPendingAndOuterRowsUntouched()
    {
        // Statement covers Jan 10 only, so decisions apply to Jan 5..Jan 15 (W = 5).
        var edge = TxOn(Acct("Chequing"), "OUT", TransactionType.Debit, new DateOnly(2026, 1, 15),
            kind: TransactionKind.PendingTransfer);
        var partnerOutside = TxOn(Acct("Savings"), "IN", TransactionType.Credit, new DateOnly(2026, 1, 18));
        var hintedOutside = TxOn(Acct("Visa"), "PAYMENT THANK YOU", TransactionType.Credit, new DateOnly(2026, 1, 19), 42m);
        StoredRows(edge, partnerOutside, hintedOutside);

        await new TransferMatchingService(_transactions)
            .RunAsync(_userId, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 10), default);

        // The edge row sees its candidate (not flipped to Regular) but is not paired with a row
        // whose own candidates were not all loaded.
        edge.Kind.Should().Be(TransactionKind.PendingTransfer);
        edge.TransferGroupId.Should().BeNull();
        partnerOutside.Kind.Should().Be(TransactionKind.Regular);
        partnerOutside.TransferGroupId.Should().BeNull();
        hintedOutside.Kind.Should().Be(TransactionKind.Regular);
    }

    [Fact]
    public async Task Matching_EdgeRowWithOnlyCandidateOutsideRange_IsNotAutoPairedWithIncompleteView()
    {
        // Jan 15 credit's only loaded candidate is the Jan 20 debit; the Jan 20 debit also has an
        // equal credit on Jan 24 that a [from - W, to + W] pool would miss.
        var edge = TxOn(Acct("Savings"), "IN", TransactionType.Credit, new DateOnly(2026, 1, 15));
        var debit = TxOn(Acct("Chequing"), "OUT", TransactionType.Debit, new DateOnly(2026, 1, 20));
        var otherCredit = TxOn(Acct("Visa"), "IN TOO", TransactionType.Credit, new DateOnly(2026, 1, 24));
        StoredRows(edge, debit, otherCredit);

        await new TransferMatchingService(_transactions)
            .RunAsync(_userId, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 10), default);

        edge.Kind.Should().Be(TransactionKind.PendingTransfer);
        edge.TransferGroupId.Should().BeNull();
        debit.Kind.Should().Be(TransactionKind.Regular);
        debit.TransferGroupId.Should().BeNull();
    }

    [Fact]
    public async Task Matching_BothLegsInsideDecisionRange_Paired()
    {
        var debit = TxOn(Acct("Chequing"), "OUT", TransactionType.Debit, new DateOnly(2026, 1, 12));
        var credit = TxOn(Acct("Savings"), "IN", TransactionType.Credit, new DateOnly(2026, 1, 15));
        var far = TxOn(Acct("Visa"), "IN FAR", TransactionType.Credit, new DateOnly(2026, 1, 21));
        StoredRows(debit, credit, far);

        await new TransferMatchingService(_transactions)
            .RunAsync(_userId, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 10), default);

        debit.Kind.Should().Be(TransactionKind.Transfer);
        credit.Kind.Should().Be(TransactionKind.Transfer);
        debit.TransferGroupId.Should().NotBeNull().And.Be(credit.TransferGroupId);
        far.Kind.Should().Be(TransactionKind.Regular);
    }
}
