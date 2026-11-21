using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Settings.GetSettings;
using FinSight.Domain.Common;
using FinSight.Domain.Exceptions;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Settings.UpdateSettings;

public sealed class UpdateSettingsCommandHandler(
    IUserRepository userRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateSettingsCommand, Result<SettingsDto>>
{
    public async Task<Result<SettingsDto>> Handle(UpdateSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<SettingsDto>(Error.Unauthorized);
        var user = await userRepository.GetByIdAsync(currentUserService.UserId.Value, cancellationToken);
        if (user is null)
            return Result.Failure<SettingsDto>(Error.Unauthorized);

        try
        {
            user.SetCorporateFiscalYearEnd(request.CorporateFiscalYearEndMonth, request.CorporateFiscalYearEndDay);
        }
        catch (DomainException ex)
        {
            return Result.Failure<SettingsDto>(Error.Custom("Settings.InvalidFiscalYearEnd", ex.Message));
        }

        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(new SettingsDto(user.CorporateFiscalYearEndMonth, user.CorporateFiscalYearEndDay));
    }
}
