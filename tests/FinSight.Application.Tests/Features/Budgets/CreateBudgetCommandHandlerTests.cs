using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Budgets.Commands.CreateBudget;
using FinSight.Application.Tests.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace FinSight.Application.Tests.Features.Budgets;

public sealed class CreateBudgetCommandHandlerTests : HandlerTestBase
{
    private readonly IBudgetRepository _budgets = Substitute.For<IBudgetRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private CreateBudgetCommandHandler CreateSut() => new(_budgets, _currentUser, _uow);

    private static readonly CreateBudgetCommand ValidCommand = new(
        TransactionCategory.GroceriesAndFood, 500m, "CAD", 6, 2025);

    [Fact]
    public async Task Handle_NoBudgetExists_ShouldCreateAndReturnId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _budgets.GetByCategoryAsync(userId, TransactionCategory.GroceriesAndFood, 6, 2025, Arg.Any<CancellationToken>())
            .ReturnsNull();

        // Act
        var result = await CreateSut().Handle(ValidCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _budgets.Received(1).AddAsync(
            Arg.Is<Budget>(b => b.Category == TransactionCategory.GroceriesAndFood && b.Limit.Amount == 500m),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateBudget_ShouldReturnConflict()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);

        // An existing budget already exists for this category/month/year
        var existing = Budget.Create(userId, TransactionCategory.GroceriesAndFood, new Money(400m, "CAD"), 6, 2025);
        _budgets.GetByCategoryAsync(userId, TransactionCategory.GroceriesAndFood, 6, 2025, Arg.Any<CancellationToken>())
            .Returns(existing);

        // Act
        var result = await CreateSut().Handle(ValidCommand, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Budget.AlreadyExists");
        await _budgets.DidNotReceive().AddAsync(Arg.Any<Budget>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_Unauthenticated_ShouldReturnUnauthorized()
    {
        // Arrange
        _currentUser.IsAuthenticated.Returns(false);
        _currentUser.UserId.Returns((Guid?)null);

        // Act
        var result = await CreateSut().Handle(ValidCommand, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Domain.Common.Error.Unauthorized);
    }
}
