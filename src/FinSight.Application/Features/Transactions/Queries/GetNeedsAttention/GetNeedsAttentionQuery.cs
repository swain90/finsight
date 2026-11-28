using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Transactions.Queries.GetNeedsAttention;

/// <summary>Lists unmatched transfer-looking transactions with their possible partners.</summary>
public sealed record GetNeedsAttentionQuery : IRequest<Result<IReadOnlyList<NeedsAttentionItemDto>>>;

public sealed record NeedsAttentionItemDto(
    Guid TransactionId,
    Guid AccountId,
    string AccountName,
    DateOnly Date,
    string Description,
    decimal Amount,
    string Type,
    IReadOnlyList<TransferCandidateDto> Candidates);

public sealed record TransferCandidateDto(
    Guid TransactionId,
    string AccountName,
    DateOnly Date,
    string Description,
    decimal Amount);
