using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Commands.CommitImport;
using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace FinSight.Application.Tests.Features.Imports;

public class CommitImportCommandHandlerTests
{
    private readonly IImportBatchRepository _batches = Substitute.For<IImportBatchRepository>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly IBalanceSnapshotRepository _snapshots = Substitute.For<IBalanceSnapshotRepository>();
    private readonly IMerchantRuleRepository _rules = Substitute.For<IMerchantRuleRepository>();
    private readonly ITransferMatchingService _matching = Substitute.For<ITransferMatchingService>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly CommitImportCommandHandler _handler;

    public CommitImportCommandHandlerTests()
    {
        _user.IsAuthenticated.Returns(true);
        _user.UserId.Returns(_userId);
        _rules.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<MerchantRule>());
        _handler = new CommitImportCommandHandler(
            _batches, _accounts, _transactions, _snapshots, _rules, _matching, _user, _uow);
    }

    private Account Acct(string name = "Chequing", AccountType type = AccountType.Checking)
    {
        var a = Account.Create(_userId, name, type, "CAD", null, Institution.Td, AccountOwner.Personal, null);
        a.ClearDomainEvents();
        return a;
    }

    private static ImportRow Row(int line, string desc, decimal amount = 10m, int day = 5,
        TransactionType type = TransactionType.Debit) =>
        ImportRow.Create(line, new DateOnly(2026, 1, day), desc, null, new Money(amount, "CAD"), type,
            TransactionCategory.Uncategorized, TransactionKind.Regular, "raw");

    private ImportBatch Batch(Account[] owned, params ImportRow[] rows)
    {
        var b = ImportBatch.Create(_userId, Institution.Td, "s.csv");
        foreach (var r in rows) b.AddRow(r);
        _batches.GetByIdAsync(b.Id, _userId, Arg.Any<CancellationToken>()).Returns(b);
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(owned.ToList());
        return b;
    }

    [Fact]
    public async Task Commit_UnassignedRow_Fails()
    {
        var acct = Acct();
        var b = Batch([acct], Row(2, "COFFEE"));

        var result = await _handler.Handle(
            new CommitImportCommand(b.Id, [new ClosingBalanceInput(acct.Id, 100m, new DateOnly(2026, 1, 31))]), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.UnassignedRows");
        result.Error.Description.Should().Be("Assign an account to every row before committing.");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Commit_MissingBalance_Fails()
    {
        var acct = Acct("Daily");
        var row = Row(2, "COFFEE");
        row.AssignAccount(acct.Id, "fp1", false);
        var b = Batch([acct], row);

        var result = await _handler.Handle(new CommitImportCommand(b.Id, []), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.MissingBalance");
        result.Error.Description.Should().Be("Enter a closing balance for Daily.");
    }

    [Fact]
    public async Task Commit_ImportsIncludedRowsOnly()
    {
        var acct = Acct();
        var kept = Row(2, "COFFEE");
        var skipped = Row(3, "TIM HORTONS");
        kept.AssignAccount(acct.Id, "fp1", false);
        skipped.AssignAccount(acct.Id, "fp2", false);
        skipped.SetExcluded(true);
        var b = Batch([acct], kept, skipped);
        List<Transaction>? added = null;
        await _transactions.AddRangeAsync(Arg.Do<IEnumerable<Transaction>>(t => added = t.ToList()), Arg.Any<CancellationToken>());

        var result = await _handler.Handle(
            new CommitImportCommand(b.Id, [new ClosingBalanceInput(acct.Id, 90m, new DateOnly(2026, 1, 31))]), default);

        result.IsSuccess.Should().BeTrue();
        added.Should().ContainSingle();
        var tx = added![0];
        tx.Description.Should().Be("COFFEE");
        tx.ImportBatchId.Should().Be(b.Id);
        tx.ImportFingerprint.Should().Be("fp1");
        tx.OccurredAt.Should().Be(new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero));
        b.Status.Should().Be(ImportBatchStatus.Committed);
        result.Value.Status.Should().Be("Committed");
    }

    [Fact]
    public async Task Commit_RecordsSnapshotAndBalance()
    {
        var acct = Acct();
        var row = Row(2, "COFFEE");
        row.AssignAccount(acct.Id, "fp1", false);
        var b = Batch([acct], row);
        BalanceSnapshot? saved = null;
        await _snapshots.AddAsync(Arg.Do<BalanceSnapshot>(s => saved = s), Arg.Any<CancellationToken>());

        await _handler.Handle(
            new CommitImportCommand(b.Id, [new ClosingBalanceInput(acct.Id, 1234.56m, new DateOnly(2026, 1, 31))]), default);

        saved.Should().NotBeNull();
        saved!.AccountId.Should().Be(acct.Id);
        saved.ImportBatchId.Should().Be(b.Id);
        saved.AsOf.Should().Be(new DateOnly(2026, 1, 31));
        saved.Balance.Amount.Should().Be(1234.56m);
        acct.Balance.Amount.Should().Be(1234.56m);
        ReferenceEquals(acct.Balance, saved.Balance).Should().BeFalse();
    }

    [Fact]
    public async Task Commit_UnbalancedAccount_StoresWarning()
    {
        var acct = Acct("Daily");
        var row = Row(2, "COFFEE", 10m);
        row.AssignAccount(acct.Id, "fp1", false);
        var b = Batch([acct], row);
        var previous = acct.RecordBalanceSnapshot(new DateOnly(2025, 12, 31), new Money(100m, "CAD"), null);
        _snapshots.GetLatestAsync(acct.Id, new DateOnly(2026, 1, 30), Arg.Any<CancellationToken>()).Returns(previous);

        // expected 100 - 10 = 90; closing says 80 => off by -10
        await _handler.Handle(
            new CommitImportCommand(b.Id, [new ClosingBalanceInput(acct.Id, 80m, new DateOnly(2026, 1, 31))]), default);

        b.ReconciliationWarning.Should().Be("Daily: off by -$10.00");
    }

    [Fact]
    public async Task Commit_FirstImport_SkippedReconciliationHasNoWarning()
    {
        var acct = Acct();
        var row = Row(2, "COFFEE");
        row.AssignAccount(acct.Id, "fp1", false);
        var b = Batch([acct], row);

        await _handler.Handle(
            new CommitImportCommand(b.Id, [new ClosingBalanceInput(acct.Id, 5m, new DateOnly(2026, 1, 31))]), default);

        b.ReconciliationWarning.Should().BeNull();
    }

    [Fact]
    public async Task Commit_CategoryChanged_UpsertsMerchantRule()
    {
        var acct = Acct();
        var fresh = Row(2, "STARBUCKS #1234");
        var existing = Row(3, "SHELL 5566");
        fresh.AssignAccount(acct.Id, "fp1", false);
        existing.AssignAccount(acct.Id, "fp2", false);
        fresh.SetCategory(TransactionCategory.DiningOut);
        existing.SetCategory(TransactionCategory.Utilities);
        var rule = MerchantRule.Create(_userId, DescriptionNormalizer.Normalize("SHELL 5566"), TransactionCategory.GroceriesAndFood);
        _rules.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<MerchantRule> { rule });
        var b = Batch([acct], fresh, existing);
        var added = new List<MerchantRule>();
        await _rules.AddAsync(Arg.Do<MerchantRule>(added.Add), Arg.Any<CancellationToken>());

        await _handler.Handle(
            new CommitImportCommand(b.Id, [new ClosingBalanceInput(acct.Id, 5m, new DateOnly(2026, 1, 31))]), default);

        added.Should().ContainSingle();
        added[0].DescriptionKey.Should().Be("STARBUCKS");
        added[0].Category.Should().Be(TransactionCategory.DiningOut);
        rule.Category.Should().Be(TransactionCategory.Utilities);
    }

    [Fact]
    public async Task Commit_RunsMatchingOverPeriod()
    {
        var acct = Acct();
        var r1 = Row(2, "A", day: 3);
        var r2 = Row(3, "B", day: 20);
        r1.AssignAccount(acct.Id, "fp1", false);
        r2.AssignAccount(acct.Id, "fp2", false);
        var b = Batch([acct], r1, r2);

        await _handler.Handle(
            new CommitImportCommand(b.Id, [new ClosingBalanceInput(acct.Id, 5m, new DateOnly(2026, 1, 31))]), default);

        await _matching.Received(1).RunAsync(_userId, new DateOnly(2026, 1, 3), new DateOnly(2026, 1, 20), Arg.Any<CancellationToken>());
        await _uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Commit_FingerprintAlreadyStored_MarksDuplicateAndFails()
    {
        var acct = Acct();
        var row = Row(2, "COFFEE");
        row.AssignAccount(acct.Id, "fp1", false);
        var b = Batch([acct], row);
        _transactions.GetExistingFingerprintsAsync(acct.Id, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string> { "fp1" });

        var result = await _handler.Handle(
            new CommitImportCommand(b.Id, [new ClosingBalanceInput(acct.Id, 5m, new DateOnly(2026, 1, 31))]), default);

        result.Error.Code.Should().Be("Import.AlreadyImported");
        result.Error.Description.Should().StartWith("1 row(s) in this import were already imported.");
        (row.IsDuplicate, row.IsExcluded).Should().Be((true, true));
        b.Status.Should().Be(ImportBatchStatus.PendingReview);
        await _transactions.DidNotReceive().AddRangeAsync(Arg.Any<IEnumerable<Transaction>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Commit_UniqueViolationOnSave_FailsWithAlreadyImported()
    {
        var acct = Acct();
        var row = Row(2, "COFFEE");
        row.AssignAccount(acct.Id, "fp1", false);
        var b = Batch([acct], row);
        _uow.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns<Task<int>>(_ => throw new FinSight.Domain.Exceptions.UniqueViolationException("x", new Exception()));

        var result = await _handler.Handle(
            new CommitImportCommand(b.Id, [new ClosingBalanceInput(acct.Id, 5m, new DateOnly(2026, 1, 31))]), default);

        result.Error.Code.Should().Be("Import.AlreadyImported");
    }

    [Fact]
    public async Task Commit_OtherUsersBatch_NotFound()
    {
        _batches.GetByIdAsync(Arg.Any<Guid>(), _userId, Arg.Any<CancellationToken>()).Returns((ImportBatch?)null);

        var result = await _handler.Handle(new CommitImportCommand(Guid.NewGuid(), []), default);

        result.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public async Task Commit_AlreadyCommitted_Fails()
    {
        var acct = Acct();
        var row = Row(2, "COFFEE");
        row.AssignAccount(acct.Id, "fp1", false);
        var b = Batch([acct], row);
        b.Commit(null);

        var result = await _handler.Handle(new CommitImportCommand(b.Id, []), default);

        result.Error.Code.Should().Be("Import.NotPending");
    }
}
