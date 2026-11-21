using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Transactions.Commands.ResolveTransfer;
using FinSight.Application.Features.Transactions.Queries.GetNeedsAttention;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace FinSight.Application.Tests.Features.Transactions;

public class NeedsAttentionTests
{
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly Guid _userId = Guid.NewGuid();
    private static readonly DateTimeOffset Jan5 = new(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);

    public NeedsAttentionTests()
    {
        _user.IsAuthenticated.Returns(true);
        _user.UserId.Returns(_userId);
        _accounts.BelongsToUserAsync(Arg.Any<Guid>(), _userId, Arg.Any<CancellationToken>()).Returns(true);
    }

    private Account Acct(string name, AccountOwner owner = AccountOwner.Personal)
    {
        var a = Account.Create(_userId, name, AccountType.Checking, "CAD", null, Institution.Other, owner, null);
        a.ClearDomainEvents();
        return a;
    }

    private static Transaction Tx(Account account, string desc, TransactionType type, decimal amount = 100m,
        TransactionKind kind = TransactionKind.PendingTransfer) =>
        account.ImportTransaction(desc, new Money(amount, "CAD"), type, Jan5, TransactionCategory.Uncategorized,
            kind, Guid.NewGuid(), Guid.NewGuid().ToString("N"));

    private void Pool(params Transaction[] pool) =>
        _transactions.GetMatchPoolAsync(_userId, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(pool.ToList());

    private ResolveTransferCommandHandler Handler() => new(_transactions, _accounts, _user, _uow);

    [Fact]
    public async Task NeedsAttention_ListsPendingWithCandidates()
    {
        var debit = Tx(Acct("Chequing"), "OUT", TransactionType.Debit);
        var c1 = Tx(Acct("Savings"), "IN ONE", TransactionType.Credit);
        var c2 = Tx(Acct("Visa"), "IN TWO", TransactionType.Credit);
        var unrelated = Tx(Acct("Other"), "ODD", TransactionType.Credit, 7m, TransactionKind.Regular);
        _transactions.GetPendingTransfersAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { debit });
        Pool(debit, c1, c2, unrelated);

        var result = await new GetNeedsAttentionQueryHandler(_transactions, _user)
            .Handle(new GetNeedsAttentionQuery(), default);

        result.IsSuccess.Should().BeTrue();
        var item = result.Value.Should().ContainSingle().Subject;
        item.TransactionId.Should().Be(debit.Id);
        item.AccountName.Should().Be("Chequing");
        item.Type.Should().Be("Debit");
        item.Date.Should().Be(new DateOnly(2026, 1, 5));
        item.Candidates.Select(c => c.TransactionId).Should().BeEquivalentTo(new[] { c1.Id, c2.Id });
        item.Candidates.Select(c => c.AccountName).Should().BeEquivalentTo(new[] { "Savings", "Visa" });
    }

    [Fact]
    public async Task Resolve_NullPartner_MarksRegularDismissed()
    {
        var tx = Tx(Acct("Chequing"), "OUT", TransactionType.Debit);
        _transactions.GetByIdAsync(tx.Id, Arg.Any<CancellationToken>()).Returns(tx);

        var result = await Handler().Handle(new ResolveTransferCommand(tx.Id, null), default);

        result.IsSuccess.Should().BeTrue();
        tx.Kind.Should().Be(TransactionKind.Regular);
        tx.IsTransferDismissed.Should().BeTrue();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_ValidPartnerCorpToPersonal_OwnerPay()
    {
        var debit = Tx(Acct("Corp", AccountOwner.Corporation), "OUT", TransactionType.Debit);
        var credit = Tx(Acct("Personal"), "IN", TransactionType.Credit);
        _transactions.GetByIdAsync(debit.Id, Arg.Any<CancellationToken>()).Returns(debit);
        Pool(debit, credit);

        var result = await Handler().Handle(new ResolveTransferCommand(debit.Id, credit.Id), default);

        result.IsSuccess.Should().BeTrue();
        debit.Kind.Should().Be(TransactionKind.OwnerPay);
        credit.Kind.Should().Be(TransactionKind.OwnerPay);
        credit.Category.Should().Be(TransactionCategory.PayFromCorporation);
        debit.TransferGroupId.Should().NotBeNull().And.Be(credit.TransferGroupId);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_ValidPartnerPersonal_Transfer()
    {
        var debit = Tx(Acct("Chequing"), "OUT", TransactionType.Debit);
        var credit = Tx(Acct("Savings"), "IN", TransactionType.Credit);
        _transactions.GetByIdAsync(debit.Id, Arg.Any<CancellationToken>()).Returns(debit);
        Pool(debit, credit);

        var result = await Handler().Handle(new ResolveTransferCommand(debit.Id, credit.Id), default);

        result.IsSuccess.Should().BeTrue();
        debit.Kind.Should().Be(TransactionKind.Transfer);
        credit.Kind.Should().Be(TransactionKind.Transfer);
    }

    [Fact]
    public async Task Resolve_NonCandidate_Fails()
    {
        var debit = Tx(Acct("Chequing"), "OUT", TransactionType.Debit);
        var wrongAmount = Tx(Acct("Savings"), "IN", TransactionType.Credit, 55m, TransactionKind.Regular);
        _transactions.GetByIdAsync(debit.Id, Arg.Any<CancellationToken>()).Returns(debit);
        Pool(debit, wrongAmount);

        var result = await Handler().Handle(new ResolveTransferCommand(debit.Id, wrongAmount.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Transfer.NotACandidate");
        result.Error.Description.Should().Be("That transaction can't be paired with this one.");
        debit.Kind.Should().Be(TransactionKind.PendingTransfer);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Resolve_OtherUsersTransaction_NotFound()
    {
        var tx = Tx(Acct("Chequing"), "OUT", TransactionType.Debit);
        _transactions.GetByIdAsync(tx.Id, Arg.Any<CancellationToken>()).Returns(tx);
        _accounts.BelongsToUserAsync(tx.AccountId, _userId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await Handler().Handle(new ResolveTransferCommand(tx.Id, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NotFound);
        tx.IsTransferDismissed.Should().BeFalse();
    }
}
