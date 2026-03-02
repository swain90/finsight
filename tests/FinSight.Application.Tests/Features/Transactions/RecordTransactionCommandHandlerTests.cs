using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Transactions.Commands.RecordTransaction;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinSight.Application.Tests.Features.Transactions;

public sealed class RecordTransactionCommandHandlerTests
{
    private readonly IAccountRepository _accountRepository = Substitute.For<IAccountRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();

    private RecordTransactionCommandHandler CreateSut() =>
        new(_accountRepository, _currentUser, _unitOfWork);

    private static Account MakeAccount(Guid userId)
    {
        var acc = Account.Create(userId, "Chequing", AccountType.Checking, "CAD");
        acc.ClearDomainEvents();
        return acc;
    }

    [Fact]
    public async Task Handle_WithOwnedAccount_RecordsTransactionAndUpdatesBalance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var account = MakeAccount(userId);
        var initialBalance = account.Balance.Amount;

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _accountRepository.BelongsToUserAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(true);
        _accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var command = new RecordTransactionCommand(
            account.Id, "Tim Hortons", 5.75m, "CAD",
            TransactionType.Debit, DateTimeOffset.UtcNow,
            TransactionCategory.Food, "Tim Hortons");

        // Act
        var result = await CreateSut().Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();

        // Balance should have decreased
        account.Balance.Amount.Should().Be(initialBalance - 5.75m);
        account.Transactions.Should().HaveCount(1);

        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUnownedAccount_ReturnsUnauthorized()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);

        // Account belongs to a DIFFERENT user — BOLA protection test
        _accountRepository.BelongsToUserAsync(accountId, userId, Arg.Any<CancellationToken>()).Returns(false);

        var command = new RecordTransactionCommand(
            accountId, "Test", 100m, "CAD",
            TransactionType.Debit, DateTimeOffset.UtcNow);

        // Act
        var result = await CreateSut().Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.Unauthorized);

        // Critical: we never fetched or modified the account
        await _accountRepository.DidNotReceive().GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithNotFoundAccount_ReturnsNotFound()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var accountId = Guid.NewGuid();

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _accountRepository.BelongsToUserAsync(accountId, userId, Arg.Any<CancellationToken>()).Returns(true);
        _accountRepository.GetByIdAsync(accountId, Arg.Any<CancellationToken>()).Returns((Account?)null);

        var command = new RecordTransactionCommand(
            accountId, "Test", 100m, "CAD",
            TransactionType.Debit, DateTimeOffset.UtcNow);

        // Act
        var result = await CreateSut().Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NotFound);
    }

    [Fact]
    public async Task Handle_CreditTransaction_IncreasesBalance()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var account = MakeAccount(userId);

        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _accountRepository.BelongsToUserAsync(account.Id, userId, Arg.Any<CancellationToken>()).Returns(true);
        _accountRepository.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var command = new RecordTransactionCommand(
            account.Id, "Salary Deposit", 5000m, "CAD",
            TransactionType.Credit, DateTimeOffset.UtcNow, TransactionCategory.Income);

        // Act
        await CreateSut().Handle(command, CancellationToken.None);

        // Assert
        account.Balance.Amount.Should().Be(5000m);
    }
}
