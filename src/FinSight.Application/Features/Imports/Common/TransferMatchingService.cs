using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;

namespace FinSight.Application.Features.Imports.Common;

/// <summary>Pairs the opposite legs of transfers between a user's accounts.</summary>
public interface ITransferMatchingService
{
    /// <summary>
    /// Re-evaluates the user's unpaired transactions dated within <see cref="TransferMatcher.WindowDays"/>
    /// (W) of the range and applies the matcher's decisions. Candidates are computed over
    /// [from − 2W, to + 2W] so every row that is judged ([from − W, to + W]) sees all of its candidates
    /// (R24). Changes are tracked but not saved.
    /// </summary>
    Task RunAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public sealed class TransferMatchingService(ITransactionRepository transactionRepository) : ITransferMatchingService
{
    public async Task RunAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        const int w = TransferMatcher.WindowDays;
        var decideFrom = from.AddDays(-w);
        var decideTo = to.AddDays(w);
        var pool = await transactionRepository.GetMatchPoolAsync(
            userId, from.AddDays(-2 * w), to.AddDays(2 * w), cancellationToken);

        // Rows the user said are not transfers are never offered to the matcher.
        var byId = pool.Where(t => !t.IsTransferDismissed).ToDictionary(t => t.Id);
        var candidates = byId.Values.Select(MatchCandidates.From).ToList();
        var dates = candidates.ToDictionary(c => c.TransactionId, c => c.Date);
        bool InDecisionRange(Guid id) => dates[id] >= decideFrom && dates[id] <= decideTo;

        var groups = new Dictionary<Guid, Guid>();
        foreach (var decision in TransferMatcher.Match(candidates))
        {
            // Rows outside [from − W, to + W] may have candidates beyond the loaded pool: leave them as they are.
            if (!InDecisionRange(decision.TransactionId))
                continue;

            var tx = byId[decision.TransactionId];
            if (decision.PartnerId is { } partnerId)
            {
                // A partner outside the decision range was judged on an incomplete view, so the pair is not
                // trusted; the row has a candidate, so it waits (visible) as PendingTransfer.
                if (!InDecisionRange(partnerId))
                {
                    if (tx.Kind != TransactionKind.PendingTransfer)
                        tx.MarkPendingTransfer();
                    continue;
                }

                var key = decision.TransactionId.CompareTo(partnerId) < 0 ? decision.TransactionId : partnerId;
                if (!groups.TryGetValue(key, out var group))
                    groups[key] = group = Guid.NewGuid();
                tx.MarkPaired(group, decision.Kind, decision.Category);
            }
            else if (decision.Kind == TransactionKind.PendingTransfer)
            {
                if (tx.Kind != TransactionKind.PendingTransfer)
                    tx.MarkPendingTransfer();
            }
            else if (tx.Kind == TransactionKind.PendingTransfer)
            {
                tx.MarkRegular();
            }
        }
    }
}
