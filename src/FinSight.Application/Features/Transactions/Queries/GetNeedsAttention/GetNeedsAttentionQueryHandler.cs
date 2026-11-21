using FinSight.Application.Common.Interfaces;
using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Transactions.Queries.GetNeedsAttention;

public sealed class GetNeedsAttentionQueryHandler(
    ITransactionRepository transactionRepository,
    ICurrentUserService currentUserService)
    : IRequestHandler<GetNeedsAttentionQuery, Result<IReadOnlyList<NeedsAttentionItemDto>>>
{
    public async Task<Result<IReadOnlyList<NeedsAttentionItemDto>>> Handle(
        GetNeedsAttentionQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<IReadOnlyList<NeedsAttentionItemDto>>(Error.Unauthorized);
        var userId = currentUserService.UserId.Value;

        var pending = await transactionRepository.GetPendingTransfersAsync(userId, cancellationToken);
        if (pending.Count == 0)
            return Result.Success<IReadOnlyList<NeedsAttentionItemDto>>([]);

        var targets = pending.Select(t => (Tx: t, Candidate: MatchCandidates.From(t))).ToList();
        var from = targets.Min(t => t.Candidate.Date).AddDays(-TransferMatcher.WindowDays);
        var to = targets.Max(t => t.Candidate.Date).AddDays(TransferMatcher.WindowDays);
        var poolTx = (await transactionRepository.GetMatchPoolAsync(userId, from, to, cancellationToken))
            .ToDictionary(t => t.Id);
        var pool = poolTx.Values.Select(MatchCandidates.From).ToList();

        var items = targets
            .OrderBy(t => t.Candidate.Date)
            .Select(t => new NeedsAttentionItemDto(
                t.Tx.Id,
                t.Tx.AccountId,
                t.Tx.Account.Name,
                t.Candidate.Date,
                t.Tx.Description,
                t.Candidate.Amount,
                t.Tx.Type.ToString(),
                TransferMatcher.FindCandidates(t.Candidate, pool)
                    .Select(c => ToCandidateDto(poolTx[c.TransactionId], c))
                    .ToList()))
            .ToList();

        return Result.Success<IReadOnlyList<NeedsAttentionItemDto>>(items);
    }

    private static TransferCandidateDto ToCandidateDto(Transaction tx, MatchCandidate c) =>
        new(tx.Id, tx.Account.Name, c.Date, tx.Description, c.Amount);
}
