using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Commands.UpdateImportRow;
using FinSight.Application.Features.Imports.Common;
using FinSight.Application.Features.Imports.Queries.CheckReconciliation;
using FinSight.Application.Features.Imports.Queries.GetImport;
using FinSight.Application.Features.Imports.Queries.GetImports;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace FinSight.Application.Tests.Features.Imports;

public class ReviewOperationsTests
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IImportBatchRepository _batches = Substitute.For<IImportBatchRepository>();
    private readonly IBalanceSnapshotRepository _snapshots = Substitute.For<IBalanceSnapshotRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly Guid _userId = Guid.NewGuid();

    public ReviewOperationsTests()
    {
        _user.IsAuthenticated.Returns(true);
        _user.UserId.Returns(_userId);
        _transactions.GetExistingFingerprintsAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<Account>());
    }

    private Account Acct(string name = "Acct")
    {
        var a = Account.Create(_userId, name, AccountType.Checking, "CAD", null, Institution.Td, AccountOwner.Personal, null);
        a.ClearDomainEvents();
        return a;
    }

    private static ImportRow Row(int line, string desc, decimal amount = 10m,
        TransactionType type = TransactionType.Debit) =>
        ImportRow.Create(line, new DateOnly(2026, 1, 5), desc, null, new Money(amount, "CAD"), type,
            TransactionCategory.Uncategorized, TransactionKind.Regular, "raw");

    private ImportBatch Batch(Guid? userId = null, params ImportRow[] rows)
    {
        var b = ImportBatch.Create(userId ?? _userId, Institution.Td, "s.csv");
        foreach (var r in rows) b.AddRow(r);
        _batches.GetByIdAsync(b.Id, userId ?? _userId, Arg.Any<CancellationToken>()).Returns(b);
        return b;
    }

    private void Owns(params Account[] accounts) =>
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(accounts.ToList());

    private UpdateImportRowCommandHandler UpdateHandler() =>
        new(_batches, _accounts, _transactions, _user, _uow);

    [Fact]
    public async Task GetImport_OtherUser_NotFound()
    {
        var b = ImportBatch.Create(Guid.NewGuid(), Institution.Td, "s.csv");
        _batches.GetByIdAsync(b.Id, _userId, Arg.Any<CancellationToken>()).Returns((ImportBatch?)null);

        var result = await new GetImportQueryHandler(_batches, _accounts, _snapshots, _user)
            .Handle(new GetImportQuery(b.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public async Task GetImports_NewestFirst()
    {
        var old = ImportBatch.Create(_userId, Institution.Td, "old.csv");
        var recent = ImportBatch.Create(_userId, Institution.Td, "new.csv");
        typeof(BaseEntity).GetProperty("CreatedAt")!.SetValue(old, DateTimeOffset.UtcNow.AddDays(-2));
        typeof(BaseEntity).GetProperty("CreatedAt")!.SetValue(recent, DateTimeOffset.UtcNow);
        _batches.GetSummariesByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<ImportBatchSummary>
        {
            new(old, 3, 2),
            new(recent, 5, 4),
        });

        var result = await new GetImportsQueryHandler(_batches, _user).Handle(new GetImportsQuery(), default);

        result.Value.Select(x => x.FileName).Should().Equal("new.csv", "old.csv");
        result.Value.Select(x => (x.RowCount, x.IncludedRowCount)).Should().Equal((5, 4), (3, 2));
    }

    [Fact]
    public async Task GetImport_Discarded_NotFound()
    {
        var b = ImportBatch.Create(_userId, Institution.Td, "s.csv");
        b.Discard();
        _batches.GetByIdAsync(b.Id, _userId, Arg.Any<CancellationToken>()).Returns(b);

        var result = await new GetImportQueryHandler(_batches, _accounts, _snapshots, _user)
            .Handle(new GetImportQuery(b.Id), default);

        result.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public async Task UpdateRow_Category_SetsChangedByUser()
    {
        var row = Row(2, "COFFEE");
        var b = Batch(null, row);

        var result = await UpdateHandler().Handle(
            new UpdateImportRowCommand(b.Id, row.Id, TransactionCategory.HousingAndRent, null, null), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Category.Should().Be("HousingAndRent");
        row.CategoryChangedByUser.Should().BeTrue();
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateRow_AssignAccount_ComputesFingerprintAndDuplicate()
    {
        var acct = Acct();
        Owns(acct);
        var first = Row(2, "COFFEE");
        var second = Row(3, "COFFEE");
        var b = Batch(null, first, second);
        first.AssignAccount(acct.Id, "x", false);
        var expected = ImportFingerprint.Compute(acct.Id, new DateOnly(2026, 1, 5), 10m,
            TransactionType.Debit, "COFFEE", 1);
        _transactions.GetExistingFingerprintsAsync(acct.Id, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string> { expected });

        var result = await UpdateHandler().Handle(
            new UpdateImportRowCommand(b.Id, second.Id, null, null, acct.Id), default);

        result.IsSuccess.Should().BeTrue();
        second.Fingerprint.Should().Be(expected);
        result.Value.AccountId.Should().Be(acct.Id);
        result.Value.IsDuplicate.Should().BeTrue();
        result.Value.IsExcluded.Should().BeTrue();
    }

    private static string Fp(Guid accountId, int occurrence) =>
        ImportFingerprint.Compute(accountId, new DateOnly(2026, 1, 5), 10m, TransactionType.Debit, "COFFEE", occurrence);

    [Fact]
    public async Task UpdateRow_AssignHigherLineFirst_DistinctFingerprints()
    {
        var acct = Acct();
        Owns(acct);
        var first = Row(2, "COFFEE");
        var second = Row(3, "COFFEE");
        var b = Batch(null, first, second);

        await UpdateHandler().Handle(new UpdateImportRowCommand(b.Id, second.Id, null, null, acct.Id), default);
        await UpdateHandler().Handle(new UpdateImportRowCommand(b.Id, first.Id, null, null, acct.Id), default);

        first.Fingerprint.Should().Be(Fp(acct.Id, 0));
        second.Fingerprint.Should().Be(Fp(acct.Id, 1));
    }

    [Fact]
    public async Task UpdateRow_MoveRowOffAccount_RenumbersRemainingRows()
    {
        var a = Acct("A");
        var other = Acct("B");
        Owns(a, other);
        var r1 = Row(2, "COFFEE");
        var r2 = Row(3, "COFFEE");
        var r3 = Row(4, "COFFEE");
        var b = Batch(null, r1, r2, r3);
        foreach (var r in new[] { r1, r2, r3 })
            await UpdateHandler().Handle(new UpdateImportRowCommand(b.Id, r.Id, null, null, a.Id), default);

        await UpdateHandler().Handle(new UpdateImportRowCommand(b.Id, r1.Id, null, null, other.Id), default);

        r2.Fingerprint.Should().Be(Fp(a.Id, 0));
        r3.Fingerprint.Should().Be(Fp(a.Id, 1));
        r1.Fingerprint.Should().Be(Fp(other.Id, 0));
    }

    [Fact]
    public async Task UpdateRow_MoveDuplicateToNonDuplicateAccount_IncludesRow()
    {
        var a = Acct("A");
        var other = Acct("B");
        Owns(a, other);
        var row = Row(2, "COFFEE");
        var b = Batch(null, row);
        _transactions.GetExistingFingerprintsAsync(a.Id, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string> { Fp(a.Id, 0) });
        await UpdateHandler().Handle(new UpdateImportRowCommand(b.Id, row.Id, null, null, a.Id), default);
        row.IsExcluded.Should().BeTrue();

        var result = await UpdateHandler().Handle(
            new UpdateImportRowCommand(b.Id, row.Id, null, null, other.Id), default);

        result.Value.IsDuplicate.Should().BeFalse();
        result.Value.IsExcluded.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateRow_AssignInactiveAccount_Fails()
    {
        var acct = Acct();
        acct.Deactivate();
        Owns(acct);
        var row = Row(2, "COFFEE");
        var b = Batch(null, row);

        var result = await UpdateHandler().Handle(
            new UpdateImportRowCommand(b.Id, row.Id, null, null, acct.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.Custom("Import.AccountInactive",
            "That account is inactive. Reactivate it or pick another account."));
        row.AccountId.Should().BeNull();
    }

    [Fact]
    public async Task UpdateRow_OtherUsersAccount_NotFound()
    {
        Owns();
        var row = Row(2, "COFFEE");
        var b = Batch(null, row);

        var result = await UpdateHandler().Handle(
            new UpdateImportRowCommand(b.Id, row.Id, null, null, Guid.NewGuid()), default);

        result.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public async Task UpdateRow_UnknownRow_NotFound()
    {
        var b = Batch(null, Row(2, "COFFEE"));

        var result = await UpdateHandler().Handle(
            new UpdateImportRowCommand(b.Id, Guid.NewGuid(), null, true, null), default);

        result.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public async Task UpdateRow_CommittedBatch_Fails()
    {
        var row = Row(2, "COFFEE");
        var b = Batch(null, row);
        b.Commit(null);

        var result = await UpdateHandler().Handle(
            new UpdateImportRowCommand(b.Id, row.Id, null, true, null), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.Custom("Import.NotPending",
            "This import has already been committed or discarded."));
        row.IsExcluded.Should().BeFalse();
    }

    [Fact]
    public async Task CheckReconciliation_UsesLatestSnapshotAndIncludedRowsOnly()
    {
        var acct = Acct();
        Owns(acct);
        var credit = Row(2, "PAY", 100m, TransactionType.Credit);
        var debit = Row(3, "COFFEE", 30m);
        var excluded = Row(4, "OTHER", 999m);
        var b = Batch(null, credit, debit, excluded);
        credit.AssignAccount(acct.Id, "a", false);
        debit.AssignAccount(acct.Id, "b", false);
        excluded.AssignAccount(acct.Id, "c", false);
        excluded.SetExcluded(true);
        _snapshots.GetLatestAsync(acct.Id, null, Arg.Any<CancellationToken>())
            .Returns(BalanceSnapshot.Create(acct.Id, new DateOnly(2025, 12, 31), new Money(500m, "CAD"), null));

        var result = await new CheckReconciliationQueryHandler(_batches, _accounts, _snapshots, _user)
            .Handle(new CheckReconciliationQuery(b.Id, acct.Id, 570m), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Skipped.Should().BeFalse();
        result.Value.Expected.Should().Be(570m);
        result.Value.IsBalanced.Should().BeTrue();
    }
}
