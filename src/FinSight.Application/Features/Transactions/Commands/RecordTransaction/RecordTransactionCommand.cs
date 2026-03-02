using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using MediatR;

namespace FinSight.Application.Features.Transactions.Commands.RecordTransaction;

public sealed record RecordTransactionCommand(
    Guid AccountId,
    string Description,
    decimal Amount,
    string Currency,
    TransactionType Type,
    DateTimeOffset OccurredAt,
    TransactionCategory Category = TransactionCategory.Uncategorized,
    string? MerchantName = null
) : IRequest<Result<Guid>>;
