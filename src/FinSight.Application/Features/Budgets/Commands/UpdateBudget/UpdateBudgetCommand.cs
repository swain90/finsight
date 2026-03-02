using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Budgets.Commands.UpdateBudget;

public sealed record UpdateBudgetCommand(Guid BudgetId, decimal NewLimit) : IRequest<Result>;
