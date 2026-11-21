using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Accounts.Commands.UpdateAccount;
using FinSight.Application.Tests.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace FinSight.Application.Tests.Features.Accounts;

public sealed class UpdateAccountCommandHandlerTests : HandlerTestBase
{
    private readonly IAccountRepository _accounts = Substitute.For<IAccountRepository>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();

    private UpdateAccountCommandHandler CreateSut() => new(_accounts, _currentUser, _uow);

    [Fact]
    public async Task Update_ChangesOwnerAndInstitution()
    {
        var userId = Guid.NewGuid();
        var account = CreateAccount(userId);
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(userId);
        _accounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var result = await CreateSut().Handle(
            new UpdateAccountCommand(account.Id, "RBC Business", AccountType.Checking,
                Institution.Rbc, AccountOwner.Corporation, "1234"),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        account.Name.Should().Be("RBC Business");
        account.Institution.Should().Be(Institution.Rbc);
        account.Owner.Should().Be(AccountOwner.Corporation);
        account.ExternalAccountRef.Should().Be("1234");
        await _uow.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_OtherUsersAccount_NotFound()
    {
        var account = CreateAccount(Guid.NewGuid());
        _currentUser.IsAuthenticated.Returns(true);
        _currentUser.UserId.Returns(Guid.NewGuid());
        _accounts.GetByIdAsync(account.Id, Arg.Any<CancellationToken>()).Returns(account);

        var result = await CreateSut().Handle(
            new UpdateAccountCommand(account.Id, "X", AccountType.Checking,
                Institution.Rbc, AccountOwner.Corporation, null),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(Error.NotFound);
        account.Owner.Should().Be(AccountOwner.Personal);
        await _uow.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
