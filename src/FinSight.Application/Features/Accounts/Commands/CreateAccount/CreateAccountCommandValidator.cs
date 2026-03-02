using FluentValidation;

namespace FinSight.Application.Features.Accounts.Commands.CreateAccount;

public sealed class CreateAccountCommandValidator : AbstractValidator<CreateAccountCommand>
{
    private static readonly string[] ValidCurrencies = ["USD", "CAD", "EUR", "GBP", "JPY"];

    public CreateAccountCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Currency).NotEmpty().Length(3).Must(c => ValidCurrencies.Contains(c.ToUpperInvariant()))
            .WithMessage("Currency must be a supported ISO code (USD, CAD, EUR, GBP, JPY).");
        RuleFor(x => x.InstitutionName).MaximumLength(200).When(x => x.InstitutionName is not null);
    }
}
