using FluentValidation;

namespace FinSight.Application.Features.Imports.Commands.CommitImport;

public sealed class CommitImportCommandValidator : AbstractValidator<CommitImportCommand>
{
    public CommitImportCommandValidator()
    {
        RuleFor(x => x.ImportId).NotEmpty();
        RuleFor(x => x.Balances).NotNull();
        RuleForEach(x => x.Balances).ChildRules(b =>
        {
            b.RuleFor(x => x.AccountId).NotEmpty();
            b.RuleFor(x => x.AsOf).NotEqual(default(DateOnly)).WithMessage("A statement date is required.");
        }).When(x => x.Balances is not null);
        RuleFor(x => x.Balances)
            .Must(b => b.Select(x => x.AccountId).Distinct().Count() == b.Count)
            .WithMessage("Enter one closing balance per account.")
            .When(x => x.Balances is not null);
    }
}
