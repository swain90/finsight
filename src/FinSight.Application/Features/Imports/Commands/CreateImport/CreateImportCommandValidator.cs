using FluentValidation;

namespace FinSight.Application.Features.Imports.Commands.CreateImport;

public sealed class CreateImportCommandValidator : AbstractValidator<CreateImportCommand>
{
    public const long MaxLength = 5 * 1024 * 1024;

    public CreateImportCommandValidator()
    {
        RuleFor(x => x.FileName)
            .Must(n => n is not null && n.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Only .csv files are supported.");
        RuleFor(x => x.Length)
            .Must(l => l > 0 && l <= MaxLength)
            .WithMessage("File must be 5 MB or smaller.");
    }
}
