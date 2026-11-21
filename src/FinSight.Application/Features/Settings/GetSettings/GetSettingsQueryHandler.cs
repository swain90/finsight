using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Settings.GetSettings;

public sealed class GetSettingsQueryHandler(IUserRepository userRepository, ICurrentUserService currentUserService)
    : IRequestHandler<GetSettingsQuery, Result<SettingsDto>>
{
    public async Task<Result<SettingsDto>> Handle(GetSettingsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<SettingsDto>(Error.Unauthorized);
        var user = await userRepository.GetByIdAsync(currentUserService.UserId.Value, cancellationToken);
        if (user is null)
            return Result.Failure<SettingsDto>(Error.Unauthorized);
        return Result.Success(new SettingsDto(user.CorporateFiscalYearEndMonth, user.CorporateFiscalYearEndDay));
    }
}
