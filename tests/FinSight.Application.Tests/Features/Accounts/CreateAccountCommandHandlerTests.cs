using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Accounts.Commands.CreateAccount;
using FinSight.Application.Tests.Common;
using FinSight.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinSight.Application.Tests.Features.Accounts;

public sealed class CreateAccountCommandHandlerTests : HandlerTestBase
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private CreateAccountCommandHandler CreateSut() => new(_accounts, _currentUser, _uow);

    private static readonly CreateAccountCommand ValidCommand =
        new("TD Chequing", AccountType.Checking, "CAD", "TD Bank");

    [Fact]
    public async Task Handle_AuthenticatedUser_ShouldCreateAccountAndReturnId()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);

        // Act
        var result = await CreateSut().Handle(ValidCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
        await _accounts.Received(1).AddAsync(Arg.Is<Domain.Entities.Account>(
            a => a.UserId == userId && a.Name == "TD Chequing"), Arg.Any<CancellationToken>());
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnauthenticatedUser_ShouldReturnUnauthorized()
    {
        // Arrange
        _currentUser.IsAuthenticated.Returns(false);
        _currentUser.UserId.Returns((Guid?)null);

        // Act
        var result = await CreateSut().Handle(ValidCommand, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Domain.Common.Error.Unauthorized);
        await _accounts.DidNotReceive().AddAsync(Arg.Any<Domain.Entities.Account>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("USD")]
    [InlineData("EUR")]
    [InlineData("GBP")]
    [InlineData("JPY")]
    public async Task Handle_SupportedCurrencies_ShouldSucceed(string currency)
    {
        // Arrange
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(Guid.NewGuid());

        var command = ValidCommand with { Currency = currency };

        // Act
        var result = await CreateSut().Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
