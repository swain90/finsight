using FinSight.Application.Features.Transactions.Commands.RecordTransaction;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Enums;
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
            : BadRequest(new ProblemDetails { Title = result.Error.Code, Detail = result.Error.Description });
    }

    private static TransactionDto MapToDto(FinSight.Domain.Entities.Transaction t)
        => new(
            t.Id,
            t.Description,
            t.Amount.Amount,
            t.Amount.Currency,
            t.Type.ToString(),
            NormalizeLegacyCategory(t.Category).ToString(),
            t.MerchantName,
            t.OccurredAt,
            t.IsAnomaly);

    private static TransactionCategory NormalizeLegacyCategory(TransactionCategory category) => category switch
    {
        (TransactionCategory)1 => TransactionCategory.GroceriesAndFood,
        (TransactionCategory)2 => TransactionCategory.Transportation,
        (TransactionCategory)3 => TransactionCategory.HousingAndRent,
        (TransactionCategory)4 => TransactionCategory.Utilities,
        (TransactionCategory)5 => TransactionCategory.HealthAndMedical,
        (TransactionCategory)6 => TransactionCategory.Entertainment,
        (TransactionCategory)7 => TransactionCategory.ShoppingAndClothing,
        (TransactionCategory)8 => TransactionCategory.Salary,
        (TransactionCategory)9 => TransactionCategory.Other,
        (TransactionCategory)10 => TransactionCategory.Other,
        (TransactionCategory)11 => TransactionCategory.EducationAndWork,
        _ => category
    };
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
    bool IsAnomaly
);
