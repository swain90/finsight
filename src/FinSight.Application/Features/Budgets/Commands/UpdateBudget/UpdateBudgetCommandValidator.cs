using FluentValidation;

namespace FinSight.Application.Features.Budgets.Commands.UpdateBudget;

public sealed class UpdateBudgetCommandValidator : AbstractValidator<UpdateBudgetCommand>
{
    public UpdateBudgetCommandValidator()
    {
        RuleFor(x => x.BudgetId).NotEmpty();
        RuleFor(x => x.NewLimit).GreaterThan(0).LessThanOrEqualTo(100_000);
    }
}
