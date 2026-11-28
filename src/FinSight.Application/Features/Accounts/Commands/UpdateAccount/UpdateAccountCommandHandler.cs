using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Accounts.Commands.UpdateAccount;

public sealed class UpdateAccountCommandHandler(
    IAccountRepository accountRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateAccountCommand, Result>
{
    public async Task<Result> Handle(UpdateAccountCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure(Error.Unauthorized);

        var account = await accountRepository.GetByIdAsync(request.AccountId, cancellationToken);
        if (account is null || account.UserId != currentUserService.UserId.Value)
            return Result.Failure(Error.NotFound);

        account.UpdateDetails(request.Name, request.Type, request.Institution, request.Owner,
            string.IsNullOrWhiteSpace(request.ExternalAccountRef) ? null : request.ExternalAccountRef.Trim());

        await accountRepository.UpdateAsync(account, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
