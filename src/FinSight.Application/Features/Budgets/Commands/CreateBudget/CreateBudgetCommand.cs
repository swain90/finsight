using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using MediatR;

namespace FinSight.Application.Features.Budgets.Commands.CreateBudget;

public sealed record CreateBudgetCommand(
    TransactionCategory Category,
    decimal LimitAmount,
    string Currency,
    int Month,
    int Year
) : IRequest<Result<Guid>>;
