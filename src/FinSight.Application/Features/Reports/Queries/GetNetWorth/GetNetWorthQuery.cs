using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Reports.Queries.GetNetWorth;

public sealed record NetWorthPointDto(DateOnly MonthEnd, decimal Personal, decimal IncludingCorporation);

public sealed record NetWorthDto(
    decimal Personal,
    decimal IncludingCorporation,
    string Currency,
    IReadOnlyList<NetWorthPointDto> History);

/// <summary>History covers the month-ends of the last <paramref name="Months"/> months, ending with the current month's end.</summary>
public sealed record GetNetWorthQuery(int Months = 12) : IRequest<Result<NetWorthDto>>;
