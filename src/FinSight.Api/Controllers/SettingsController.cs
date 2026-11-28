using FinSight.Api.Extensions;
using FinSight.Application.Features.Settings.GetSettings;
using FinSight.Application.Features.Settings.UpdateSettings;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

public sealed record UpdateSettingsRequest(int CorporateFiscalYearEndMonth, int CorporateFiscalYearEndDay);

[ApiController]
[Route("api/v1/settings")]
[Authorize]
public class SettingsController(IMediator mediator) : ControllerBase
{
    /// <summary>Get the user's settings</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await mediator.Send(new GetSettingsQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }

    /// <summary>Update the corporate fiscal year end</summary>
    [HttpPut]
    [ProducesResponseType(typeof(SettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Update([FromBody] UpdateSettingsRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateSettingsCommand(request.CorporateFiscalYearEndMonth, request.CorporateFiscalYearEndDay), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }
}
