using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Commands.CreateImport;
using FinSight.Application.Features.Imports.Common;
using FinSight.Application.Tests.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FluentAssertions;
using NSubstitute;

namespace FinSight.Application.Tests.Features.Imports;

public class CreateImportCommandHandlerTests : HandlerTestBase
{
    private readonly IStatementParser _parser = Substitute.For<IStatementParser>();
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly IImportBatchRepository _batches = Substitute.For<IImportBatchRepository>();
    private readonly IMerchantRuleRepository _rules = Substitute.For<IMerchantRuleRepository>();
    private readonly IBalanceSnapshotRepository _snapshots = Substitute.For<IBalanceSnapshotRepository>();
    private readonly ITransactionRepository _transactions = Substitute.For<ITransactionRepository>();
    private readonly ITransactionCategorizationService _categorizer = Substitute.For<ITransactionCategorizationService>();
    private readonly ICurrentUserService _user = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly CreateImportCommandHandler _handler;

    public CreateImportCommandHandlerTests()
    {
        _user.IsAuthenticated.Returns(true);
        _user.UserId.Returns(_userId);
        _rules.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(new List<MerchantRule>());
        _transactions.GetExistingFingerprintsAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string>());
        _categorizer.Categorize(Arg.Any<string>(), Arg.Any<string>()).Returns(TransactionCategory.Uncategorized);
        _handler = new CreateImportCommandHandler(_parser, _accounts, _batches, _rules, _snapshots,
            _transactions, _categorizer, _user, _uow);
    }

    private static ParsedStatementRow Row(int line, string desc, decimal amount = 10m, string? aref = null,
        TransactionKind kind = TransactionKind.Regular, TransactionCategory? hint = null) =>
        new(line, new DateOnly(2026, 1, 5), desc, null, amount, TransactionType.Debit, "CAD", aref, kind, hint, "raw");

    private Account Acct(Institution inst, string? aref = null, string name = "Acct")
    {
        var a = Account.Create(_userId, name, AccountType.Checking, "CAD", null, inst, AccountOwner.Personal, aref);
        a.ClearDomainEvents();
        return a;
    }

    private void Setup(Institution inst, IReadOnlyList<ParsedStatementRow> rows, params Account[] accounts)
    {
        _parser.Parse(Arg.Any<Stream>()).Returns(Result.Success(new ParsedStatement(inst, rows)));
        _accounts.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(accounts.ToList());
    }

    private static CreateImportCommand Cmd(Guid? accountId = null) =>
        new("s.csv", 10, new MemoryStream([1]), accountId);

    [Fact]
    public async Task Handle_SingleMatchingAccount_AssignsAndFingerprints()
    {
        var acct = Acct(Institution.Td);
        Setup(Institution.Td, [Row(2, "COFFEE")], acct);

        var result = await _handler.Handle(Cmd(), default);

        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Rows.Single();
        row.AccountId.Should().Be(acct.Id);
        result.Value.Status.Should().Be("PendingReview");
        result.Value.Accounts.Should().ContainSingle(a => a.AccountId == acct.Id);
        await _batches.Received(1).AddAsync(Arg.Any<ImportBatch>(), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _transactions.Received(1).GetExistingFingerprintsAsync(acct.Id,
            Arg.Is<IReadOnlyCollection<string>>(f => f.Count == 1 && f.First().Length == 64), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExistingFingerprint_MarksDuplicateAndExcluded()
    {
        var acct = Acct(Institution.Td);
        Setup(Institution.Td, [Row(2, "COFFEE")], acct);
        var fp = ImportFingerprint.Compute(acct.Id, new DateOnly(2026, 1, 5), 10m, TransactionType.Debit, "COFFEE", 0);
        _transactions.GetExistingFingerprintsAsync(acct.Id, Arg.Any<IReadOnlyCollection<string>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<string> { fp });

        var result = await _handler.Handle(Cmd(), default);

        var row = result.Value.Rows.Single();
        row.IsDuplicate.Should().BeTrue();
        row.IsExcluded.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_TwoAccountsSameInstitution_LeavesUnassigned()
    {
        var a1 = Acct(Institution.Td, name: "A1");
        var a2 = Acct(Institution.Td, name: "A2");
        Setup(Institution.Td, [Row(2, "COFFEE")], a1, a2);

        var result = await _handler.Handle(Cmd(), default);

        result.Value.Rows.Single().AccountId.Should().BeNull();
        result.Value.Accounts.Select(a => a.AccountId).Should().BeEquivalentTo([a1.Id, a2.Id]);
    }

    [Fact]
    public async Task Handle_ExternalRefDisambiguates()
    {
        var a1 = Acct(Institution.Td, "1234", "A1");
        var a2 = Acct(Institution.Td, "9999", "A2");
        Setup(Institution.Td, [Row(2, "COFFEE", aref: "1234"), Row(3, "TEA", aref: "9999")], a1, a2);

        var result = await _handler.Handle(Cmd(), default);

        result.Value.Rows[0].AccountId.Should().Be(a1.Id);
        result.Value.Rows[1].AccountId.Should().Be(a2.Id);
    }

    [Fact]
    public async Task Handle_AccountIdOfOtherUser_ReturnsNotFound()
    {
        Setup(Institution.Td, [Row(2, "COFFEE")]);
        var other = Guid.NewGuid();
        _accounts.BelongsToUserAsync(other, _userId, Arg.Any<CancellationToken>()).Returns(false);

        var result = await _handler.Handle(Cmd(other), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NotFound);
        await _batches.DidNotReceive().AddAsync(Arg.Any<ImportBatch>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_MerchantRuleBeatsCategorizer()
    {
        var acct = Acct(Institution.Td);
        Setup(Institution.Td, [Row(2, "Coffee #123 Shop", hint: TransactionCategory.ShoppingAndClothing)], acct);
        _rules.GetByUserIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(new List<MerchantRule> { MerchantRule.Create(_userId, "COFFEE SHOP", TransactionCategory.DiningOut) });
        _categorizer.Categorize(Arg.Any<string>(), Arg.Any<string>()).Returns(TransactionCategory.GroceriesAndFood);

        var result = await _handler.Handle(Cmd(), default);

        result.Value.Rows.Single().Category.Should().Be(TransactionCategory.DiningOut.ToString());
    }

    [Fact]
    public async Task Handle_PaymentThankYou_KindHintPendingTransfer()
    {
        var acct = Acct(Institution.Mbna);
        Setup(Institution.Mbna, [Row(2, "PAYMENT THANK YOU")], acct);

        var result = await _handler.Handle(Cmd(), default);

        result.Value.Rows.Single().KindHint.Should().Be(TransactionKind.PendingTransfer.ToString());
    }

    [Fact]
    public async Task Handle_ParserFailure_ReturnsFailureAndSavesNothing()
    {
        var err = Error.Custom("Import.Format", "Unrecognized file.");
        _parser.Parse(Arg.Any<Stream>()).Returns(Result.Failure<ParsedStatement>(err));

        var result = await _handler.Handle(Cmd(), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(err);
        await _batches.DidNotReceive().AddAsync(Arg.Any<ImportBatch>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExplicitInactiveAccount_FailsAndSavesNothing()
    {
        var acct = Acct(Institution.Td);
        acct.Deactivate();
        Setup(Institution.Td, [Row(2, "COFFEE")], acct);
        _accounts.BelongsToUserAsync(acct.Id, _userId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(Cmd(acct.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Import.AccountInactive");
        result.Error.Description.Should().Be("That account is inactive. Reactivate it or pick another account.");
        await _batches.DidNotReceive().AddAsync(Arg.Any<ImportBatch>(), Arg.Any<CancellationToken>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ExplicitAccountId_AssignsEveryRow()
    {
        var a1 = Acct(Institution.Td, "1234", "A1");
        var a2 = Acct(Institution.Td, "9999", "A2");
        Setup(Institution.Td, [Row(2, "COFFEE", aref: "1234"), Row(3, "TEA", aref: "9999")], a1, a2);
        _accounts.BelongsToUserAsync(a2.Id, _userId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _handler.Handle(Cmd(a2.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value.Rows.Should().OnlyContain(r => r.AccountId == a2.Id);
    }
}
