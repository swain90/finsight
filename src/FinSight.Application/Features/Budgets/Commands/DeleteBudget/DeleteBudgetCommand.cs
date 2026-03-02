using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Budgets.Commands.DeleteBudget;

public sealed record DeleteBudgetCommand(Guid BudgetId) : IRequest<Result>;
