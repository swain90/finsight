using FinSight.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Extensions;

public static class ResultExtensions
{
    /// <summary>
    /// Maps a failed <see cref="Error"/> to the HTTP status documented on the controllers:
    /// validation 422, unauthorized 401, not found 404, conflict / already-exists / in-use 409,
    /// anything else 400.
    /// </summary>
    public static ObjectResult ToProblemResult(this ControllerBase controller, Error error)
    {
        var status = error.Code switch
        {
            var c when c.StartsWith("Validation", StringComparison.Ordinal) => StatusCodes.Status422UnprocessableEntity,
            var c when c.StartsWith("Unauthorized", StringComparison.Ordinal) => StatusCodes.Status401Unauthorized,
            var c when c.StartsWith("NotFound", StringComparison.Ordinal) => StatusCodes.Status404NotFound,
            var c when c.StartsWith("Conflict", StringComparison.Ordinal)
                || c.EndsWith("AlreadyExists", StringComparison.Ordinal)
                || c.EndsWith("EmailInUse", StringComparison.Ordinal) => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        };

        return new ObjectResult(new ProblemDetails
        {
            Status = status,
            Title = error.Code,
            Detail = error.Description
        })
        { StatusCode = status };
    }
}
