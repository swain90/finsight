using FinSight.Domain.Enums;

namespace FinSight.Application.Features.Imports.Common;

/// <summary>One transaction offered to the transfer matcher.</summary>
public sealed record MatchCandidate(
    Guid TransactionId,
    Guid AccountId,
    AccountOwner Owner,
    DateOnly Date,
    decimal Amount,
    TransactionType Type,
    bool HasTransferHint);

/// <summary>The matcher's verdict for one transaction.</summary>
public sealed record MatchDecision(
    Guid TransactionId,
    Guid? PartnerId,
    TransactionKind Kind,
    TransactionCategory? Category);

/// <summary>
/// Decides which transactions are the two legs of one transfer between accounts.
/// Description hints only break ties; they never create a pair on their own.
/// </summary>
public static class TransferMatcher
{
    /// <summary>Maximum date difference, in days (inclusive), between the two legs.</summary>
    public const int WindowDays = 5;

    /// <summary>Words in a normalized description that suggest a transfer or payment.</summary>
    public static readonly string[] HintWords = ["PAYMENT", "THANK YOU", "TRANSFER", "WEALTHSIMPLE", "E TRANSFER"];

    /// <summary>Whether a normalized (uppercase, punctuation-free) description contains a hint word.</summary>
    public static bool HasHint(string normalizedDescription) =>
        HintWords.Any(w => normalizedDescription.Contains(w, StringComparison.Ordinal));

    /// <summary>
    /// Candidates for <paramref name="target"/>: a different account, same amount,
    /// opposite type and within <see cref="WindowDays"/> days.
    /// </summary>
    public static IReadOnlyList<MatchCandidate> FindCandidates(MatchCandidate target, IReadOnlyList<MatchCandidate> pool) =>
        pool.Where(c =>
                c.TransactionId != target.TransactionId
                && c.AccountId != target.AccountId
                && c.Amount == target.Amount
                && c.Type != target.Type
                && Math.Abs(c.Date.DayNumber - target.Date.DayNumber) <= WindowDays)
            .ToList();

    /// <summary>Returns exactly one decision per pool item; each transaction is paired at most once.</summary>
    public static IReadOnlyList<MatchDecision> Match(IReadOnlyList<MatchCandidate> pool)
    {
        var ordered = pool.OrderBy(c => c.Date).ThenBy(c => c.TransactionId).ToList();
        var candidates = ordered.ToDictionary(c => c.TransactionId, c => FindCandidates(c, ordered));
        var partners = new Dictionary<Guid, MatchCandidate>();

        foreach (var a in ordered)
        {
            if (partners.ContainsKey(a.TransactionId))
            {
                continue;
            }

            var partner = FindPartner(a, candidates, partners);
            if (partner is not null)
            {
                partners[a.TransactionId] = partner;
                partners[partner.TransactionId] = a;
            }
        }

        return ordered.Select(c => Decide(c, partners, candidates)).ToList();
    }

    /// <summary>
    /// The kind and category <paramref name="self"/> takes once paired with <paramref name="partner"/>:
    /// a Corporation debit paired with a Personal credit is OwnerPay (the credit leg is categorized
    /// PayFromCorporation); every other pair is a plain Transfer.
    /// </summary>
    public static (TransactionKind Kind, TransactionCategory? Category) DecidePair(MatchCandidate self, MatchCandidate partner)
    {
        var debit = self.Type == TransactionType.Debit ? self : partner;
        var credit = self.Type == TransactionType.Credit ? self : partner;
        var ownerPay = debit.Owner == AccountOwner.Corporation && credit.Owner == AccountOwner.Personal;
        if (!ownerPay)
        {
            return (TransactionKind.Transfer, null);
        }

        var category = self.Type == TransactionType.Credit ? TransactionCategory.PayFromCorporation : (TransactionCategory?)null;
        return (TransactionKind.OwnerPay, category);
    }

    private static MatchCandidate? FindPartner(
        MatchCandidate a,
        Dictionary<Guid, IReadOnlyList<MatchCandidate>> candidates,
        Dictionary<Guid, MatchCandidate> partners)
    {
        var aCands = candidates[a.TransactionId];
        if (aCands.Count == 0)
        {
            return null;
        }

        // Mutual only-candidates.
        if (aCands.Count == 1)
        {
            var b = aCands[0];
            if (!partners.ContainsKey(b.TransactionId) && OnlyCandidateIs(b, a, candidates))
            {
                return b;
            }
        }

        // Tie-break: A is ambiguous, but exactly one candidate is hinted and is exclusive to A.
        var hinted = aCands.Where(c => c.HasTransferHint).ToList();
        if (hinted.Count == 1)
        {
            var h = hinted[0];
            if (!partners.ContainsKey(h.TransactionId) && OnlyCandidateIs(h, a, candidates))
            {
                return h;
            }
        }

        // Mirror tie-break: A is hinted and exclusive to its only candidate B, who is the ambiguous one.
        if (aCands.Count == 1 && a.HasTransferHint)
        {
            var b = aCands[0];
            var bHinted = candidates[b.TransactionId].Where(c => c.HasTransferHint).ToList();
            if (!partners.ContainsKey(b.TransactionId) && bHinted.Count == 1 && bHinted[0].TransactionId == a.TransactionId)
            {
                return b;
            }
        }

        return null;
    }

    private static bool OnlyCandidateIs(
        MatchCandidate owner,
        MatchCandidate expected,
        Dictionary<Guid, IReadOnlyList<MatchCandidate>> candidates)
    {
        var list = candidates[owner.TransactionId];
        return list.Count == 1 && list[0].TransactionId == expected.TransactionId;
    }

    private static MatchDecision Decide(
        MatchCandidate c,
        Dictionary<Guid, MatchCandidate> partners,
        Dictionary<Guid, IReadOnlyList<MatchCandidate>> candidates)
    {
        if (partners.TryGetValue(c.TransactionId, out var p))
        {
            var (kind, category) = DecidePair(c, p);
            return new MatchDecision(c.TransactionId, p.TransactionId, kind, category);
        }

        var pending = candidates[c.TransactionId].Count > 0 || c.HasTransferHint;
        return new MatchDecision(c.TransactionId, null, pending ? TransactionKind.PendingTransfer : TransactionKind.Regular, null);
    }
}
