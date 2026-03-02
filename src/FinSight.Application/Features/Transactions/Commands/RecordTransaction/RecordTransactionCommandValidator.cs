using FluentValidation;

namespace FinSight.Application.Features.Transactions.Commands.RecordTransaction;

public sealed class RecordTransactionCommandValidator : AbstractValidator<RecordTransactionCommand>
{
    public RecordTransactionCommandValidator()
    {
        RuleFor(x => x.AccountId).NotEmpty();
        RuleFor(x => x.Description).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Amount).GreaterThan(0).LessThanOrEqualTo(1_000_000);
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.Type).IsInEnum();
        RuleFor(x => x.OccurredAt).LessThanOrEqualTo(DateTimeOffset.UtcNow.AddDays(1));
        RuleFor(x => x.MerchantName).MaximumLength(200).When(x => x.MerchantName is not null);
    }
}
