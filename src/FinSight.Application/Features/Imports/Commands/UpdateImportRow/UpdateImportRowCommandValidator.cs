using FluentValidation;

namespace FinSight.Application.Features.Imports.Commands.UpdateImportRow;

public sealed class UpdateImportRowCommandValidator : AbstractValidator<UpdateImportRowCommand>
{
    public UpdateImportRowCommandValidator()
    {
        RuleFor(x => x.ImportId).NotEmpty();
        RuleFor(x => x.RowId).NotEmpty();
        RuleFor(x => x.Category!.Value).IsInEnum().When(x => x.Category is not null);
        RuleFor(x => x.AccountId!.Value).NotEmpty().When(x => x.AccountId is not null);
    }
}
