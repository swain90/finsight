using FinSight.Api.Extensions;
using FinSight.Application.Features.Accounts.Commands.CreateAccount;
using FinSight.Application.Features.Accounts.Commands.UpdateAccount;
using FinSight.Application.Features.Accounts.Queries.GetAccounts;
using FinSight.Domain.ValueObjects;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AccountsController(IMediator mediator) : ControllerBase
{
    /// <summary>Get all accounts for the authenticated user</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AccountDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAccounts(CancellationToken ct)
    {
        var result = await mediator.Send(new GetAccountsQuery(), ct);
        return result.IsSuccess ? Ok(result.Value) : Unauthorized();
    }

    /// <summary>Create a new financial account</summary>
    [HttpPost]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> CreateAccount([FromBody] CreateAccountRequest request, CancellationToken ct)
    {
        var command = new CreateAccountCommand(request.Name, request.Type, request.Currency, request.InstitutionName,
            request.Institution, request.Owner, request.ExternalAccountRef);
        var result = await mediator.Send(command, ct);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAccounts), new { id = result.Value }, result.Value)
            : this.ToProblemResult(result.Error);
    }

    /// <summary>Update an account's name, type, institution, owner and external reference</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] UpdateAccountRequest request, CancellationToken ct)
    {
        var command = new UpdateAccountCommand(id, request.Name, request.Type, request.Institution,
            request.Owner, request.ExternalAccountRef);
        var result = await mediator.Send(command, ct);
        return result.IsSuccess ? NoContent() : this.ToProblemResult(result.Error);
    }
}

public sealed record CreateAccountRequest(
    string Name,
    FinSight.Domain.Enums.AccountType Type,
    string Currency = Money.DefaultCurrency,
    string? InstitutionName = null,
    FinSight.Domain.Enums.Institution Institution = FinSight.Domain.Enums.Institution.Other,
    FinSight.Domain.Enums.AccountOwner Owner = FinSight.Domain.Enums.AccountOwner.Personal,
    string? ExternalAccountRef = null
);

public sealed record UpdateAccountRequest(
    string Name,
    FinSight.Domain.Enums.AccountType Type,
    FinSight.Domain.Enums.Institution Institution = FinSight.Domain.Enums.Institution.Other,
    FinSight.Domain.Enums.AccountOwner Owner = FinSight.Domain.Enums.AccountOwner.Personal,
    string? ExternalAccountRef = null
);
