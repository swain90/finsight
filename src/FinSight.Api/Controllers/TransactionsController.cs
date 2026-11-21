using FinSight.Api.Extensions;
using FinSight.Application.Features.Transactions.Commands.RecordTransaction;
using FinSight.Application.Features.Transactions.Commands.ResolveTransfer;
using FinSight.Application.Features.Transactions.Queries.GetNeedsAttention;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Repositories;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class TransactionsController(
    IMediator mediator,
    ITransactionRepository transactionRepository,
    IAccountRepository accountRepository,
    ICurrentUserService currentUserService) : ControllerBase
{
    /// <summary>Get transactions for the authenticated user across all accounts (paginated)</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetAllTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Unauthorized();

        if (page < 1 || pageSize < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ValidationError",
                Detail = "page and pageSize must be greater than 0."
            });
        }

        var transactions = await transactionRepository.GetByUserIdPagedAsync(currentUserService.UserId.Value, page, pageSize, ct);
        var dtos = transactions.Select(MapToDto).ToList().AsReadOnly();
        return Ok(dtos);
    }

    /// <summary>Get transactions for an account (paginated)</summary>
    [HttpGet("account/{accountId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<TransactionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetTransactions(
        Guid accountId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Unauthorized();

        if (page < 1 || pageSize < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "ValidationError",
                Detail = "page and pageSize must be greater than 0."
            });
        }

        var belongsToUser = await accountRepository.BelongsToUserAsync(accountId, currentUserService.UserId.Value, ct);
        if (!belongsToUser)
            return Unauthorized();

        var transactions = await transactionRepository.GetByAccountIdAsync(accountId, page, pageSize, ct);
        var dtos = transactions.Select(MapToDto).ToList().AsReadOnly();

        return Ok(dtos);
    }

    /// <summary>Record a new transaction</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    public async Task<IActionResult> RecordTransaction([FromBody] RecordTransactionCommand command, CancellationToken ct)
    {
        var result = await mediator.Send(command, ct);
        return result.IsSuccess
            ? CreatedAtAction(nameof(GetTransactions), new { accountId = command.AccountId }, result.Value)
            : this.ToProblemResult(result.Error);
    }

    /// <summary>Unmatched transfer-looking transactions with their possible partners</summary>
    [HttpGet("needs-attention")]
    [ProducesResponseType(typeof(IReadOnlyList<NeedsAttentionItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetNeedsAttention(CancellationToken ct)
    {
        var result = await mediator.Send(new GetNeedsAttentionQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblemResult(result.Error);
    }

    /// <summary>Pair a pending transfer with a partner, or mark it as not a transfer (null partner)</summary>
    [HttpPost("{id:guid}/resolve-transfer")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResolveTransfer(Guid id, [FromBody] ResolveTransferRequest request, CancellationToken ct)
    {
        var result = await mediator.Send(new ResolveTransferCommand(id, request.PartnerId), ct);
        return result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error);
    }

    private static TransactionDto MapToDto(FinSight.Domain.Entities.Transaction t)
        => new(
            t.Id,
            t.Description,
            t.Amount.Amount,
            t.Amount.Currency,
            t.Type.ToString(),
            t.Category.ToString(),
            t.MerchantName,
            t.OccurredAt,
            t.IsAnomaly,
            t.Kind.ToString());
}

public sealed record TransactionDto(
    Guid Id,
    string Description,
    decimal Amount,
    string Currency,
    string Type,
    string Category,
    string? MerchantName,
    DateTimeOffset OccurredAt,
    bool IsAnomaly,
    string Kind
);

public sealed record ResolveTransferRequest(Guid? PartnerId);
