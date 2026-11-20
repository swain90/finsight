using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Auth.Commands.Login;
using FinSight.Application.Tests.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace FinSight.Application.Tests.Features.Auth;

public sealed class LoginCommandHandlerTests : HandlerTestBase
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IJwtService _jwt = Substitute.For<IJwtService>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();

    private readonly IApplicationDbContext _db = CreateDbContext();

    private LoginCommandHandler CreateSut() => new(_users, _uow, _db, _jwt, _hasher);

    private static readonly LoginCommand ValidCommand =
        new("alice@example.com", "Password@1", "127.0.0.1");

    [Fact]
    public async Task Handle_ValidCredentials_ShouldReturnTokens()
    {
        // Arrange
        var user = CreateUser(email: "alice@example.com", passwordHash: "$2a$12$real-hash");
        _users.GetByEmailAsync("alice@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("Password@1", "$2a$12$real-hash").Returns(true);
        _jwt.GenerateAccessToken(user).Returns("access.tok");
        _jwt.GenerateRefreshToken(user.Id, "127.0.0.1")
            .Returns(RefreshToken.Create(user.Id, "refresh.tok", DateTimeOffset.UtcNow.AddDays(30), "127.0.0.1"));

        // Act
        var result = await CreateSut().Handle(ValidCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access.tok");
        result.Value.RefreshToken.Should().Be("refresh.tok");
    }

    [Fact]
    public async Task Handle_WrongPassword_ShouldReturnInvalidCredentials()
    {
        // Arrange
        var user = CreateUser(email: "alice@example.com", passwordHash: "$2a$12$real-hash");
        _users.GetByEmailAsync("alice@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("WrongPassword", "$2a$12$real-hash").Returns(false);

        var command = ValidCommand with { Password = "WrongPassword" };

        // Act
        var result = await CreateSut().Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownEmail_ShouldReturnSameErrorAsWrongPassword()
    {
        // Arrange — user not found; hash check still runs (timing protection)
        _users.GetByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .ReturnsNull();
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        // Act
        var result = await CreateSut().Handle(ValidCommand, CancellationToken.None);

        // Assert — same error code to prevent email enumeration
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");

        // Verify hashing still called even when user not found (timing safety)
        _hasher.Received(1).Verify(Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task Handle_DeactivatedUser_ShouldReturnInvalidCredentials()
    {
        // Arrange
        var user = CreateUser(email: "deactivated@example.com");
        user.Deactivate();
        _users.GetByEmailAsync("deactivated@example.com", Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        var command = ValidCommand with { Email = "deactivated@example.com" };

        // Act
        var result = await CreateSut().Handle(command, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.InvalidCredentials");
    }
}
