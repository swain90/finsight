using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Accounts.Queries.GetAccounts;

public sealed class GetAccountsQueryHandler(
    IAccountRepository accountRepository,
    ICurrentUserService currentUserService) : IRequestHandler<GetAccountsQuery, Result<IReadOnlyList<AccountDto>>>
{
    public async Task<Result<IReadOnlyList<AccountDto>>> Handle(GetAccountsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<IReadOnlyList<AccountDto>>(Error.Unauthorized);

        var accounts = await accountRepository.GetByUserIdAsync(currentUserService.UserId.Value, cancellationToken);

        var dtos = accounts.Select(a => new AccountDto(
            a.Id, a.Name, a.Type.ToString(),
            a.Balance.Amount, a.Balance.Currency,
            a.InstitutionName, a.IsActive, a.LastSyncedAt, a.CreatedAt,
            a.Institution.ToString(), a.Owner.ToString(), a.ExternalAccountRef,
            a.Type.IsLiability(), a.Type.IsInvestment()
        )).ToList().AsReadOnly();

        return Result.Success<IReadOnlyList<AccountDto>>(dtos);
    }
}
