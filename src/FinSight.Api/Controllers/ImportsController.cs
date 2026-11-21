using FinSight.Api.Extensions;
using FinSight.Application.Features.Imports.Commands.CommitImport;
using FinSight.Application.Features.Imports.Commands.CreateImport;
using FinSight.Application.Features.Imports.Commands.DeleteImport;
using FinSight.Application.Features.Imports.Commands.UpdateImportRow;
using FinSight.Application.Features.Imports.Common;
using FinSight.Application.Features.Imports.Queries.CheckReconciliation;
using FinSight.Application.Features.Imports.Queries.GetImport;
using FinSight.Application.Features.Imports.Queries.GetImports;
using FinSight.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class ImportsController(IMediator mediator) : ControllerBase
{
    /// <summary>Upload a CSV statement and parse it into a pending import</summary>
    [HttpPost]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ImportBatchDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromForm] Guid? accountId, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        var result = await mediator.Send(new CreateImportCommand(file.FileName, file.Length, stream, accountId), ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetImport), new { id = result.Value.Id }, result.Value)
            : this.ToProblemResult(result.Error);
    }

    /// <summary>List the user's imports</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ImportBatchSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetImports(CancellationToken ct)
    {
        var result = await mediator.Send(new GetImportsQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }

    /// <summary>Get one import with its rows</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ImportBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetImport(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new GetImportQuery(id), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }

    /// <summary>Update a row's category, exclusion or account</summary>
    [HttpPatch("{id:guid}/rows/{rowId:guid}")]
    [ProducesResponseType(typeof(ImportRowDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRow(
        Guid id, Guid rowId, [FromBody] UpdateImportRowRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(
            new UpdateImportRowCommand(id, rowId, request.Category, request.IsExcluded, request.AccountId), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }

    /// <summary>Compare statement closing balance against the computed balance</summary>
    [HttpGet("{id:guid}/reconciliation")]
    [ProducesResponseType(typeof(ReconciliationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CheckReconciliation(
        Guid id, [FromQuery] Guid accountId, [FromQuery] decimal closingBalance, CancellationToken ct)
    {
        var result = await mediator.Send(new CheckReconciliationQuery(id, accountId, closingBalance), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }

    /// <summary>Commit a pending import</summary>
    [HttpPost("{id:guid}/commit")]
    [ProducesResponseType(typeof(ImportBatchDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Commit(Guid id, [FromBody] CommitImportRequest request, CancellationToken ct)
    {
        var balances = (request.Balances ?? [])
            .Select(b => new ClosingBalanceInput(b.AccountId, b.ClosingBalance, b.AsOf))
            .ToList();
        var result = await mediator.Send(new CommitImportCommand(id, balances), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }

    /// <summary>Discard a pending import, or undo a committed one</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await mediator.Send(new DeleteImportCommand(id), ct);
        return result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error);
    }
}

public sealed record UpdateImportRowRequest(TransactionCategory? Category, bool? IsExcluded, Guid? AccountId);

public sealed record CommitImportRequest(IReadOnlyList<ClosingBalanceRequest>? Balances);

public sealed record ClosingBalanceRequest(Guid AccountId, decimal ClosingBalance, DateOnly AsOf);
