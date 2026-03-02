using FinSight.Application.Features.Insights.Queries.GetSpendingInsights;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class InsightsController(IMediator mediator) : ControllerBase
{
    /// <summary>Get AI-powered spending insights for a given month</summary>
    [HttpGet("spending/{year:int}/{month:int}")]
    [ProducesResponseType(typeof(SpendingInsightsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSpendingInsights(int year, int month, CancellationToken ct)
    {
        if (month < 1 || month > 12)
            return BadRequest(new ProblemDetails { Title = "Invalid month", Detail = "Month must be between 1 and 12." });

        var result = await mediator.Send(new GetSpendingInsightsQuery(month, year), ct);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized();
    }
}
