using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using MediatR;

namespace FinSight.Application.Features.Accounts.Commands.UpdateAccount;

public sealed record UpdateAccountCommand(
    Guid AccountId,
    string Name,
    AccountType Type,
    Institution Institution,
    AccountOwner Owner,
    string? ExternalAccountRef
) : IRequest<Result>;
