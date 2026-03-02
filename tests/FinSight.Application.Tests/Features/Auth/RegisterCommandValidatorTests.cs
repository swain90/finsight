using FinSight.Application.Features.Auth.Commands.Register;
using FluentAssertions;
using FluentValidation;
using Xunit;

namespace FinSight.Application.Tests.Features.Auth;

public sealed class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Theory]
    [InlineData("valid@example.com", "P@ssword1!", "Alice", "Smith", true)]
    [InlineData("not-an-email",       "P@ssword1!", "Alice", "Smith", false)] // bad email
    [InlineData("valid@example.com", "short1!",   "Alice", "Smith", false)]  // too short
    [InlineData("valid@example.com", "nouppercase1!", "Alice", "Smith", false)]
    [InlineData("valid@example.com", "NOLOWER1!",     "Alice", "Smith", false)]
    [InlineData("valid@example.com", "NoNumber!",     "Alice", "Smith", false)]
    [InlineData("valid@example.com", "NoSpecial1",    "Alice", "Smith", false)]
    [InlineData("valid@example.com", "P@ssword1!", "", "Smith", false)]  // empty first name
    [InlineData("valid@example.com", "P@ssword1!", "Alice", "", false)]  // empty last name
    public async Task Validate_VariousInputs_ReturnsExpectedResult(
        string email, string password, string firstName, string lastName, bool expectedValid)
    {
        var command = new RegisterCommand(email, password, firstName, lastName);
        var result = await _validator.ValidateAsync(command);

        if (expectedValid)
            result.IsValid.Should().BeTrue();
        else
            result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Validate_PasswordMeetsAllRequirements_Passes()
    {
        var command = new RegisterCommand("test@example.com", "SecureP@ss1!", "Test", "User");
        var result = await _validator.ValidateAsync(command);
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Validate_EmptyEmail_HasEmailError()
    {
        var command = new RegisterCommand("", "P@ssword1!", "Test", "User");
        var result = await _validator.ValidateAsync(command);
        result.Errors.Should().Contain(e => e.PropertyName == nameof(RegisterCommand.Email));
    }
}
