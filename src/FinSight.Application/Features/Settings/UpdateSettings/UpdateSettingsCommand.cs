using FinSight.Application.Features.Settings.GetSettings;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Settings.UpdateSettings;

public sealed record UpdateSettingsCommand(int CorporateFiscalYearEndMonth, int CorporateFiscalYearEndDay)
    : IRequest<Result<SettingsDto>>;
