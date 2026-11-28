using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Accounts.Queries.GetAccounts;

public sealed record GetAccountsQuery : IRequest<Result<IReadOnlyList<AccountDto>>>;

public sealed record AccountDto(
    Guid Id,
    string Name,
    string Type,
    decimal Balance,
    string Currency,
    string? InstitutionName,
    bool IsActive,
    DateTimeOffset? LastSyncedAt,
    DateTimeOffset CreatedAt,
    string Institution,
    string Owner,
    string? ExternalAccountRef,
    bool IsLiability,
    bool IsInvestment
);
