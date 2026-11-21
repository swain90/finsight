using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Settings.GetSettings;

public sealed record SettingsDto(int CorporateFiscalYearEndMonth, int CorporateFiscalYearEndDay);

public sealed record GetSettingsQuery : IRequest<Result<SettingsDto>>;
