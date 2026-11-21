using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Reports.Queries.GetCorporationSummary;

public sealed record CorporationMonthDto(int Year, int Month, decimal Revenue, decimal PaidToOwner, decimal Expenses);

public sealed record CorporationSummaryDto(
    DateOnly FiscalYearStart,
    DateOnly FiscalYearEnd,
    decimal Revenue,
    decimal PaidToOwner,
    decimal Expenses,
    decimal ClosingBalance,
    IReadOnlyList<CorporationMonthDto> Months);

/// <summary>FiscalYear is the year the fiscal year ends in; null means the one containing today.</summary>
public sealed record GetCorporationSummaryQuery(int? FiscalYear) : IRequest<Result<CorporationSummaryDto>>;
