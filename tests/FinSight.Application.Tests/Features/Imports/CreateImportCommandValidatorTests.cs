using FinSight.Application.Features.Imports.Commands.CreateImport;
using FluentAssertions;

namespace FinSight.Application.Tests.Features.Imports;

public class CreateImportCommandValidatorTests
{
    private const long FiveMb = 5 * 1024 * 1024;
    private readonly CreateImportCommandValidator _validator = new();

    private static CreateImportCommand Cmd(string name, long length) =>
        new(name, length, Stream.Null, null);

    [Fact]
    public void Validate_TxtFile_Rejected()
    {
        var result = _validator.Validate(Cmd("statement.txt", 100));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "Only .csv or .pdf files are supported.");
    }

    [Fact]
    public void Validate_PdfFile_Accepted() =>
        _validator.Validate(Cmd("statement.PDF", 100)).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_UppercaseCsvExtension_Accepted() =>
        _validator.Validate(Cmd("STATEMENT.CSV", 100)).IsValid.Should().BeTrue();

    [Fact]
    public void Validate_ZeroBytes_Rejected()
    {
        var result = _validator.Validate(Cmd("a.csv", 0));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "File must be 5 MB or smaller.");
    }

    [Fact]
    public void Validate_OverFiveMb_Rejected()
    {
        var result = _validator.Validate(Cmd("a.csv", FiveMb + 1));

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.ErrorMessage == "File must be 5 MB or smaller.");
    }

    [Fact]
    public void Validate_ExactlyFiveMb_Accepted() =>
        _validator.Validate(Cmd("a.csv", FiveMb)).IsValid.Should().BeTrue();
}
