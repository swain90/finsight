using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using MediatR;

namespace FinSight.Application.Features.Accounts.Commands.CreateAccount;

public sealed record CreateAccountCommand(
    string Name,
    AccountType Type,
    string Currency,
    string? InstitutionName,
    Institution Institution = Institution.Other,
    AccountOwner Owner = AccountOwner.Personal,
    string? ExternalAccountRef = null
) : IRequest<Result<Guid>>;
