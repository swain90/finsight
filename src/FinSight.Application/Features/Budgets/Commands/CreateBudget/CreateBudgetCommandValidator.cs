using FluentValidation;

namespace FinSight.Application.Features.Budgets.Commands.CreateBudget;

public sealed class CreateBudgetCommandValidator : AbstractValidator<CreateBudgetCommand>
{
    public CreateBudgetCommandValidator()
    {
        RuleFor(x => x.Category)
            .IsInEnum()
            .NotEqual(FinSight.Domain.Enums.TransactionCategory.Uncategorized)
                .WithMessage("A budget category must be specified.");

        RuleFor(x => x.LimitAmount)
            .GreaterThan(0).WithMessage("Budget limit must be greater than zero.")
            .LessThanOrEqualTo(100_000).WithMessage("Budget limit cannot exceed 100,000.");

        RuleFor(x => x.Currency)
            .NotEmpty().Length(3);

        RuleFor(x => x.Month)
            .InclusiveBetween(1, 12).WithMessage("Month must be between 1 and 12.");

        RuleFor(x => x.Year)
            .InclusiveBetween(2000, 2100).WithMessage("Year must be between 2000 and 2100.");
    }
}
