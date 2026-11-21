using FinSight.Api.Extensions;
using FinSight.Domain.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.IntegrationTests.Tests;

/// <summary>Pins the Error.Code to HTTP status mapping so new error codes cannot silently fall to 400.</summary>
public sealed class ResultExtensionsTests
{
    private sealed class StubController : ControllerBase;

    public static TheoryData<Error, int> Cases => new()
    {
        { Error.NotFound, 404 },
        { Error.Unauthorized, 401 },
        { Error.Conflict, 409 },
        { Error.Validation, 422 },
        { Error.Custom("Validation.Failed", "x"), 422 },
        { Error.Custom("Budget.AlreadyExists", "x"), 409 },
        { Error.Custom("Auth.EmailInUse", "x"), 409 },
        { Error.Custom("Conflict.Something", "x"), 409 },
        { Error.Custom("Anything.Else", "x"), 400 },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public void ToProblemResult_MapsErrorCodeToStatus(Error error, int expected)
    {
        var result = new StubController().ToProblemResult(error);

        result.StatusCode.Should().Be(expected);
        var problem = result.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(expected);
        problem.Title.Should().Be(error.Code);
    }
}
