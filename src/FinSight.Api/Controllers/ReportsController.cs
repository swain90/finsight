using FinSight.Api.Extensions;
using FinSight.Application.Features.Reports.Queries.GetCorporationSummary;
using FinSight.Application.Features.Reports.Queries.GetNetWorth;
using FinSight.Application.Features.Reports.Queries.GetPersonalIncome;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/v1/reports")]
[Authorize]
public class ReportsController(IMediator mediator) : ControllerBase
{
    /// <summary>Net worth now plus month-end history (the last point is the current month's end)</summary>
    [HttpGet("net-worth")]
    [ProducesResponseType(typeof(NetWorthDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNetWorth([FromQuery] int months = 12, CancellationToken ct = default)
    {
        var result = await mediator.Send(new GetNetWorthQuery(months), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }

    /// <summary>Corporation revenue, owner pay and expenses for a fiscal year (default: the current one)</summary>
    [HttpGet("corporation")]
    [ProducesResponseType(typeof(CorporationSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCorporationSummary([FromQuery] int? fiscalYear, CancellationToken ct)
    {
        var result = await mediator.Send(new GetCorporationSummaryQuery(fiscalYear), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }

    /// <summary>Personal income for a calendar year (default: the current year)</summary>
    [HttpGet("personal-income")]
    [ProducesResponseType(typeof(PersonalIncomeDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPersonalIncome([FromQuery] int? year, CancellationToken ct)
    {
        var result = await mediator.Send(new GetPersonalIncomeQuery(year), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }
}
