using NetArchTest.Rules;
using Xunit;

namespace FinSight.ArchitectureTests;

public class ArchitectureTests
{
    private const string DomainNamespace = "FinSight.Domain";
    private const string ApplicationNamespace = "FinSight.Application";
    private const string InfrastructureNamespace = "FinSight.Infrastructure";
    private const string ApiNamespace = "FinSight.Api";

    [Fact]
    public void Domain_ShouldNot_HaveDependencyOnApplication()
    {
        var result = Types.InNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn(ApplicationNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain layer references Application: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_ShouldNot_HaveDependencyOnInfrastructure()
    {
        var result = Types.InNamespace(DomainNamespace)
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Domain layer references Infrastructure: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_ShouldNot_HaveDependencyOnInfrastructure()
    {
        var result = Types.InNamespace(ApplicationNamespace)
            .ShouldNot().HaveDependencyOn(InfrastructureNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Application layer references Infrastructure: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_ShouldNot_HaveDependencyOnApi()
    {
        var result = Types.InNamespace(ApplicationNamespace)
            .ShouldNot().HaveDependencyOn(ApiNamespace)
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Application layer references API: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void CommandHandlers_ShouldBeSealed()
    {
        var result = Types.InNamespace(ApplicationNamespace)
            .That().HaveNameEndingWith("CommandHandler")
            .Should().BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Non-sealed handlers found: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Validators_ShouldBeSealed()
    {
        var result = Types.InNamespace(ApplicationNamespace)
            .That().HaveNameEndingWith("Validator")
            .Should().BeSealed()
            .GetResult();

        Assert.True(result.IsSuccessful,
            $"Non-sealed validators found: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
