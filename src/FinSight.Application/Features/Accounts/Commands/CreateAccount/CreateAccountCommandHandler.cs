using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandHandler(
    IAccountRepository accountRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateAccountCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateAccountCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<Guid>(Error.Unauthorized);

        var account = Account.Create(
            currentUserService.UserId.Value,
            request.Name,
            request.Type,
            request.Currency,
            request.InstitutionName,
            request.Institution,
            request.Owner,
            request.ExternalAccountRef);

        await accountRepository.AddAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(account.Id);
    }
}
