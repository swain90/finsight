using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Commands.DeleteImport;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace FinSight.Application.Tests.Features.Imports;

public class DeleteImportCommandHandlerTests
{
    private readonly IImportBatchRepository _batches = Substitute.For<IImportBatchRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IBalanceSnapshotRepository _snapshots = Substitute.For<IBalanceSnapshotRepository>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DeleteImportCommandHandler _handler;

    public DeleteImportCommandHandlerTests()
    {
        _user.IsAuthenticated.Returns(true);
        _user.UserId.Returns(_userId);
        _handler = new DeleteImportCommandHandler(_batches, _accounts, _transactions, _snapshots, _user, _uow);
    }

    private ImportBatch NewBatch()
    {
        var b = ImportBatch.Create(_userId, Institution.Td, "s.csv");
        _batches.GetByIdAsync(b.Id, _userId, Arg.Any<CancellationToken>()).Returns(b);
        return b;
    }

    [Fact]
    public async Task Delete_Pending_Discards()
    {
        var b = NewBatch();

        var result = await _handler.Handle(new DeleteImportCommand(b.Id), default);

        result.IsSuccess.Should().BeTrue();
        b.Status.Should().Be(ImportBatchStatus.Discarded);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_Discarded_Fails()
    {
        var b = NewBatch();
        b.Discard();

        var result = await _handler.Handle(new DeleteImportCommand(b.Id), default);

        result.Error.Code.Should().Be("Import.AlreadyDiscarded");
        result.Error.Description.Should().Be("This import was already removed.");
    }

    [Fact]
    public async Task Delete_OtherUser_NotFound()
    {
        _batches.GetByIdAsync(Arg.Any<Guid>(), _userId, Arg.Any<CancellationToken>()).Returns((ImportBatch?)null);

        var result = await _handler.Handle(new DeleteImportCommand(Guid.NewGuid()), default);

        result.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public async Task Delete_Committed_RemovesTransactionsAndSnapshotsAndResetsBalance()
    {
        var b = NewBatch();
        b.Commit(null);
        var acct = Account.Create(_userId, "Chequing", AccountType.Checking, "CAD", null, Institution.Td, AccountOwner.Personal, null);
        var other = Account.Create(_userId, "Savings", AccountType.Savings, "CAD", null, Institution.Other, AccountOwner.Personal, null);
        var older = acct.RecordBalanceSnapshot(new DateOnly(2025, 12, 31), new Money(500m, "CAD"), null);
        var batchSnapshot = acct.RecordBalanceSnapshot(new DateOnly(2026, 1, 31), new Money(900m, "CAD"), b.Id);
        var occurred = new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero);
        var mine = acct.ImportTransaction("OUT", new Money(100m, "CAD"), TransactionType.Debit, occurred,
            TransactionCategory.Uncategorized, TransactionKind.Regular, b.Id, "fp1");
        var partner = other.ImportTransaction("IN", new Money(100m, "CAD"), TransactionType.Credit, occurred,
            TransactionCategory.Uncategorized, TransactionKind.Regular, Guid.NewGuid(), "fp2");
        var group = Guid.NewGuid();
        mine.MarkPaired(group, TransactionKind.Transfer);
        partner.MarkPaired(group, TransactionKind.Transfer);
        _transactions.GetByImportBatchIdAsync(b.Id, Arg.Any<CancellationToken>()).Returns(new List<Transaction> { mine });
        _transactions.GetByTransferGroupIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(new List<Transaction> { mine, partner });
        _snapshots.GetByImportBatchIdAsync(b.Id, Arg.Any<CancellationToken>()).Returns(new List<BalanceSnapshot> { batchSnapshot });
        _snapshots.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<BalanceSnapshot> { older, batchSnapshot });
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<Account> { acct, other });

        var result = await _handler.Handle(new DeleteImportCommand(b.Id), default);

        result.IsSuccess.Should().BeTrue();
        _transactions.Received(1).Remove(mine);
        _transactions.DidNotReceive().Remove(partner);
        _snapshots.Received(1).Remove(batchSnapshot);
        partner.Kind.Should().Be(TransactionKind.PendingTransfer);
        partner.TransferGroupId.Should().BeNull();
        acct.Balance.Amount.Should().Be(500m);
        acct.LatestSnapshotAsOf.Should().Be(new DateOnly(2025, 12, 31));
        b.Status.Should().Be(ImportBatchStatus.Discarded);
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
