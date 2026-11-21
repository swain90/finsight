using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;

namespace FinSight.Application.Features.Imports.Common;

/// <summary>Pairs the opposite legs of transfers between a user's accounts.</summary>
public interface ITransferMatchingService
{
    /// <summary>
    /// Re-evaluates the user's unpaired transactions dated within <see cref="TransferMatcher.WindowDays"/>
    /// of the range and applies the matcher's decisions. Changes are tracked but not saved.
    /// </summary>
    Task RunAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken);
}

public sealed class TransferMatchingService(ITransactionRepository transactionRepository) : ITransferMatchingService
{
    public async Task RunAsync(Guid userId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var pool = await transactionRepository.GetMatchPoolAsync(
            userId, from.AddDays(-TransferMatcher.WindowDays), to.AddDays(TransferMatcher.WindowDays), cancellationToken);

        // Rows the user said are not transfers are never offered to the matcher.
        var byId = pool.Where(t => !t.IsTransferDismissed).ToDictionary(t => t.Id);
        var candidates = byId.Values.Select(t => new MatchCandidate(
            t.Id,
            t.AccountId,
            t.Account.Owner,
            DateOnly.FromDateTime(t.OccurredAt.UtcDateTime),
            t.Amount.Amount,
            t.Type,
            TransferMatcher.HasHint(DescriptionNormalizer.Normalize(t.Description)))).ToList();

        var groups = new Dictionary<Guid, Guid>();
        foreach (var decision in TransferMatcher.Match(candidates))
        {
            var tx = byId[decision.TransactionId];
            if (decision.PartnerId is { } partnerId)
            {
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
