using FluentValidation;

namespace FinSight.Application.Features.Accounts.Commands.UpdateAccount;

public sealed class UpdateAccountCommandValidator : AbstractValidator<UpdateAccountCommand>
{
    public UpdateAccountCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.Institution).IsInEnum();
        RuleFor(x => x.Owner).IsInEnum();
        RuleFor(x => x.ExternalAccountRef).MaximumLength(50).When(x => x.ExternalAccountRef is not null);
    }
}
