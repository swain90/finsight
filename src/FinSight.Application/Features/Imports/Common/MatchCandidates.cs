using FinSight.Domain.Entities;

namespace FinSight.Application.Features.Imports.Common;

/// <summary>Turns stored transactions into <see cref="MatchCandidate"/>s for <see cref="TransferMatcher"/>.</summary>
public static class MatchCandidates
{
    /// <summary>Requires <see cref="Transaction.Account"/> to be loaded. The date is the UTC statement date (R7).</summary>
    public static MatchCandidate From(Transaction t) => new(
        t.Id,
        t.AccountId,
        t.Account.Owner,
        DateOnly.FromDateTime(t.OccurredAt.UtcDateTime),
        t.Amount.Amount,
        t.Type,
        TransferMatcher.HasHint(DescriptionNormalizer.Normalize(t.Description)));
}
