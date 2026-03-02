using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using MediatR;

namespace FinSight.Application.Features.Accounts.Commands.CreateAccount;

public sealed record CreateAccountCommand(
    string Name,
    AccountType Type,
    string Currency,
    string? InstitutionName
) : IRequest<Result<Guid>>;
