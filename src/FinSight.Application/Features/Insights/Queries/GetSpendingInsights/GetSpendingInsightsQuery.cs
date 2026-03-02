using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Insights.Queries.GetSpendingInsights;

public sealed record GetSpendingInsightsQuery(int Month, int Year) : IRequest<Result<SpendingInsightsDto>>;

public sealed record SpendingInsightsDto(
    int Month,
    int Year,
    decimal TotalSpent,
    decimal TotalIncome,
    decimal NetFlow,
    string Currency,
    IReadOnlyList<CategoryBreakdownDto> CategoryBreakdown,
    IReadOnlyList<string> AiInsights,
    int AnomalyCount
);

public sealed record CategoryBreakdownDto(
    string Category,
    decimal Amount,
    int TransactionCount,
    decimal PercentageOfTotal
);
