using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Application.Features.Auth.Commands.Register;
using FinSight.Application.Tests.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using NSubstitute.ReturnsExtensions;
using Xunit;

namespace FinSight.Application.Tests.Features.Auth;

public sealed class RegisterCommandHandlerTests : HandlerTestBase
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly IJwtService _jwt = Substitute.For<IJwtService>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();

    private RegisterCommandHandler CreateSut() => new(_users, _uow, _jwt, _hasher);

    private static readonly RegisterCommand ValidCommand = new(
        "alice@example.com", "Password@1", "Alice", "Nguyen");

    [Fact]
    public async Task Handle_NewEmail_ShouldCreateUserAndReturnTokens()
    {
        // Arrange
        _users.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        _hasher.Hash("Password@1").Returns("$2a$12$hashed");
        _jwt.GenerateAccessToken(Arg.Any<Domain.Entities.User>()).Returns("access.jwt.token");
        _jwt.GenerateRefreshToken(Arg.Any<string>())
            .Returns(Domain.Entities.RefreshToken.Create(Guid.NewGuid(), "refresh-token", DateTimeOffset.UtcNow.AddDays(30)));

        // Act
        var result = await CreateSut().Handle(ValidCommand, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AccessToken.Should().Be("access.jwt.token");
        result.Value.Email.Should().Be("alice@example.com");
        result.Value.RefreshToken.Should().Be("refresh-token");

        // Password was hashed before being passed to User.Create
        _hasher.Received(1).Hash("Password@1");
        await _uow.Received(2).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DuplicateEmail_ShouldReturnConflictFailure()
    {
        // Arrange
        _users.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(true);

        // Act
        var result = await CreateSut().Handle(ValidCommand, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("Auth.EmailInUse");

        // Nothing should have been persisted
        _hasher.DidNotReceive().Hash(Arg.Any<string>());
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_ShouldNeverStoreRawPassword()
    {
        // Arrange — hasher returns a mock hash
        _users.ExistsByEmailAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        const string rawPassword = "SuperSecret@1";
        const string fakeHash = "$2a$12$MOCKHASH";
        _hasher.Hash(rawPassword).Returns(fakeHash);
        _jwt.GenerateAccessToken(Arg.Any<Domain.Entities.User>()).Returns("tok");
        _jwt.GenerateRefreshToken(Arg.Any<string>())
            .Returns(Domain.Entities.RefreshToken.Create(Guid.NewGuid(), "rt", DateTimeOffset.UtcNow.AddDays(30)));

        var command = ValidCommand with { Password = rawPassword };

        Domain.Entities.User? capturedUser = null;
        await _users.AddAsync(Arg.Do<Domain.Entities.User>(u => capturedUser = u), Arg.Any<CancellationToken>());

        // Act
        await CreateSut().Handle(command, CancellationToken.None);

        // Assert: the User stored in the repo has the HASH, not the raw password
        capturedUser.Should().NotBeNull();
        capturedUser!.PasswordHash.Should().Be(fakeHash);
        capturedUser.PasswordHash.Should().NotBe(rawPassword);
    }
}
