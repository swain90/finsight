using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Budgets.Queries.GetBudgetSummary;

public sealed record GetBudgetSummaryQuery(int Month, int Year) : IRequest<Result<BudgetSummaryDto>>;

public sealed record BudgetSummaryDto(
    int Month,
    int Year,
    int TotalBudgets,
    int BudgetsOnTrack,
    int BudgetsOverLimit,
    decimal TotalBudgeted,
    decimal TotalSpent,
    decimal TotalRemaining,
    string Currency,
    decimal OverallSpendingPercentage,
    IReadOnlyList<OverBudgetAlertDto> Alerts
);

public sealed record OverBudgetAlertDto(
    string Category,
    decimal LimitAmount,
    decimal SpentAmount,
    decimal OverageAmount,
    decimal OveragePercentage
);
