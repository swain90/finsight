using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using MediatR;

namespace FinSight.Application.Features.Budgets.Commands.UpdateBudget;

public sealed class UpdateBudgetCommandHandler(
    IBudgetRepository budgetRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateBudgetCommand, Result>
{
    public async Task<Result> Handle(UpdateBudgetCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure(Error.Unauthorized);

        var budget = await budgetRepository.GetByIdAsync(request.BudgetId, cancellationToken);
        if (budget is null)
            return Result.Failure(Error.NotFound);

        if (budget.UserId != currentUserService.UserId.Value)
            return Result.Failure(Error.Unauthorized);

        budget.UpdateLimit(new Money(request.NewLimit, budget.Limit.Currency));
        await budgetRepository.UpdateAsync(budget, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
