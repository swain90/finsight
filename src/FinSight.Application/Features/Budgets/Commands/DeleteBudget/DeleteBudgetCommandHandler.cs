using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Budgets.Commands.DeleteBudget;

public sealed class DeleteBudgetCommandHandler(
    IBudgetRepository budgetRepository,
    ICurrentUserService currentUserService,
    IApplicationDbContext dbContext,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteBudgetCommand, Result>
{
    public async Task<Result> Handle(DeleteBudgetCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure(Error.Unauthorized);

        var budget = await budgetRepository.GetByIdAsync(request.BudgetId, cancellationToken);
        if (budget is null)
            return Result.Failure(Error.NotFound);

        // Another user's budget is reported as not found so its existence is not disclosed.
        if (budget.UserId != currentUserService.UserId.Value)
            return Result.Failure(Error.NotFound);

        dbContext.Budgets.Remove(budget);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
