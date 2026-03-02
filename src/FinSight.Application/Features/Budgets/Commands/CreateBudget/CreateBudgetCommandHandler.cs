using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using MediatR;

namespace FinSight.Application.Features.Budgets.Commands.CreateBudget;

public sealed class CreateBudgetCommandHandler(
    IBudgetRepository budgetRepository,
    ICurrentUserService currentUserService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateBudgetCommand, Result<Guid>>
{
    public async Task<Result<Guid>> Handle(CreateBudgetCommand request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<Guid>(Error.Unauthorized);

        // Prevent duplicate budgets for same category/month/year
        var existing = await budgetRepository.GetByCategoryAsync(
            currentUserService.UserId.Value,
            request.Category,
            request.Month,
            request.Year,
            cancellationToken);

        if (existing is not null)
            return Result.Failure<Guid>(Error.Custom(
                "Budget.AlreadyExists",
                $"A budget for {request.Category} in {request.Month}/{request.Year} already exists."));

        var budget = Budget.Create(
            currentUserService.UserId.Value,
            request.Category,
            new Money(request.LimitAmount, request.Currency),
            request.Month,
            request.Year);

        await budgetRepository.AddAsync(budget, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(budget.Id);
    }
}
