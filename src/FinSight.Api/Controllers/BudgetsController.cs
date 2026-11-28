using FinSight.Api.Extensions;
using FinSight.Application.Features.Budgets.Commands.CreateBudget;
using FinSight.Application.Features.Budgets.Commands.DeleteBudget;
using FinSight.Application.Features.Budgets.Commands.UpdateBudget;
using FinSight.Application.Features.Budgets.Queries.GetBudgets;
using FinSight.Application.Features.Budgets.Queries.GetBudgetSummary;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class BudgetsController(IMediator mediator) : ControllerBase
{
    /// <summary>Get all budgets for a given month</summary>
    [HttpGet("{year:int}/{month:int}")]
    [ProducesResponseType(typeof(IReadOnlyList<BudgetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBudgets(int year, int month, CancellationToken ct)
    {
        var result = await mediator.Send(new GetBudgetsQuery(month, year), ct);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized();
    }

    /// <summary>Get budget summary with over-budget alerts</summary>
    [HttpGet("{year:int}/{month:int}/summary")]
    [ProducesResponseType(typeof(BudgetSummaryDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSummary(int year, int month, CancellationToken ct)
    {
        var result = await mediator.Send(new GetBudgetSummaryQuery(month, year), ct);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized();
    }

    /// <summary>Create a new budget for a category</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateBudget([FromBody] CreateBudgetCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        return CreatedAtAction(nameof(GetBudgets),
            new { year = command.Year, month = command.Month }, result.Value);
    }

    /// <summary>Update the limit for an existing budget</summary>
    [HttpPut("{budgetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateBudget(Guid budgetId, [FromBody] UpdateLimitRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new UpdateBudgetCommand(budgetId, request.NewLimit), ct);

        return result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error);
    }

    /// <summary>Delete a budget</summary>
    [HttpDelete("{budgetId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteBudget(Guid budgetId, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteBudgetCommand(budgetId), ct);

        return result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error);
    }
}

public sealed record UpdateLimitRequest(decimal NewLimit);
