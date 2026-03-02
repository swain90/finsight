using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Budgets.Queries.GetBudgets;

public sealed record GetBudgetsQuery(int Month, int Year) : IRequest<Result<IReadOnlyList<BudgetDto>>>;

public sealed record BudgetDto(
    Guid Id,
    string Category,
    decimal LimitAmount,
    decimal SpentAmount,
    decimal RemainingAmount,
    string Currency,
    decimal SpendingPercentage,
    bool IsOverBudget,
    int Month,
    int Year
);
