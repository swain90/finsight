using FinSight.Application.Features.Imports.Common;
using FinSight.Domain.Enums;
using FluentAssertions;

namespace FinSight.Application.Tests.Features.Imports;

public class TransferMatcherTests
{
    private static readonly Guid AccA = Guid.NewGuid();
    private static readonly Guid AccB = Guid.NewGuid();
    private static readonly Guid AccC = Guid.NewGuid();

    private static MatchCandidate C(Guid account, AccountOwner owner, int day, decimal amount,
        TransactionType type, bool hint = false) =>
        new(Guid.NewGuid(), account, owner, new DateOnly(2026, 10, day), amount, type, hint);

    private static MatchDecision For(IReadOnlyList<MatchDecision> d, MatchCandidate c) =>
        d.Single(x => x.TransactionId == c.TransactionId);

    [Fact]
    public void Match_UniqueOppositeWithinWindow_PairsAsTransfer()
    {
        var debit = C(AccA, AccountOwner.Personal, 5, 500m, TransactionType.Debit);
        var credit = C(AccB, AccountOwner.Personal, 6, 500m, TransactionType.Credit);

        var d = TransferMatcher.Match([debit, credit]);

        For(d, debit).Should().Be(new MatchDecision(debit.TransactionId, credit.TransactionId, TransactionKind.Transfer, null));
        For(d, credit).Should().Be(new MatchDecision(credit.TransactionId, debit.TransactionId, TransactionKind.Transfer, null));
    }

    [Fact]
    public void Match_CorpDebitToPersonalCredit_IsOwnerPayWithCategory()
    {
        var debit = C(AccA, AccountOwner.Corporation, 5, 3000m, TransactionType.Debit);
        var credit = C(AccB, AccountOwner.Personal, 5, 3000m, TransactionType.Credit);

        var d = TransferMatcher.Match([debit, credit]);

        For(d, debit).Kind.Should().Be(TransactionKind.OwnerPay);
        For(d, debit).Category.Should().BeNull();
        For(d, credit).Kind.Should().Be(TransactionKind.OwnerPay);
        For(d, credit).Category.Should().Be(TransactionCategory.PayFromCorporation);
    }

    [Fact]
    public void Match_PersonalDebitToCorpCredit_NeverAutoPairedBothPending()
    {
        // R23: owner money into the corporation is rare and must be confirmed by the owner.
        var debit = C(AccA, AccountOwner.Personal, 5, 800m, TransactionType.Debit);
        var credit = C(AccB, AccountOwner.Corporation, 5, 800m, TransactionType.Credit);

        var d = TransferMatcher.Match([debit, credit]);

        For(d, debit).Should().Be(new MatchDecision(debit.TransactionId, null, TransactionKind.PendingTransfer, null));
        For(d, credit).Should().Be(new MatchDecision(credit.TransactionId, null, TransactionKind.PendingTransfer, null));
    }

    [Fact]
    public void Match_PersonalDebitToCorpCreditWithHints_StillNotAutoPaired()
    {
        var debit = C(AccA, AccountOwner.Personal, 5, 800m, TransactionType.Debit, hint: true);
        var credit = C(AccB, AccountOwner.Corporation, 6, 800m, TransactionType.Credit, hint: true);

        var d = TransferMatcher.Match([debit, credit]);

        d.Should().OnlyContain(x => x.PartnerId == null && x.Kind == TransactionKind.PendingTransfer);
    }

    [Fact]
    public void DecidePair_PersonalDebitToCorpCredit_IsTransfer()
    {
        // Used by ResolveTransfer once the owner confirms the pair.
        var debit = C(AccA, AccountOwner.Personal, 5, 800m, TransactionType.Debit);
        var credit = C(AccB, AccountOwner.Corporation, 5, 800m, TransactionType.Credit);

        TransferMatcher.DecidePair(debit, credit).Should().Be((TransactionKind.Transfer, (TransactionCategory?)null));
        TransferMatcher.DecidePair(credit, debit).Should().Be((TransactionKind.Transfer, (TransactionCategory?)null));
    }

    [Fact]
    public void Match_SixDaysApart_NotPaired()
    {
        var debit = C(AccA, AccountOwner.Personal, 1, 100m, TransactionType.Debit);
        var credit = C(AccB, AccountOwner.Personal, 7, 100m, TransactionType.Credit);

        var d = TransferMatcher.Match([debit, credit]);

        d.Should().OnlyContain(x => x.Kind == TransactionKind.Regular && x.PartnerId == null);
    }

    [Fact]
    public void Match_FiveDaysApart_IsPaired()
    {
        var debit = C(AccA, AccountOwner.Personal, 1, 100m, TransactionType.Debit);
        var credit = C(AccB, AccountOwner.Personal, 6, 100m, TransactionType.Credit);

        TransferMatcher.Match([debit, credit]).Should().OnlyContain(x => x.Kind == TransactionKind.Transfer);
    }

    [Fact]
    public void Match_TwoCandidatesNoHint_BothSidesPending()
    {
        var debit = C(AccA, AccountOwner.Personal, 5, 100m, TransactionType.Debit);
        var c1 = C(AccB, AccountOwner.Personal, 5, 100m, TransactionType.Credit);
        var c2 = C(AccC, AccountOwner.Personal, 6, 100m, TransactionType.Credit);

        var d = TransferMatcher.Match([debit, c1, c2]);

        d.Should().OnlyContain(x => x.Kind == TransactionKind.PendingTransfer && x.PartnerId == null);
    }

    [Fact]
    public void Match_TwoCandidatesOneHinted_PairsHinted()
    {
        var debit = C(AccA, AccountOwner.Personal, 5, 100m, TransactionType.Debit);
        var plain = C(AccB, AccountOwner.Personal, 5, 100m, TransactionType.Credit);
        var hinted = C(AccC, AccountOwner.Personal, 6, 100m, TransactionType.Credit, hint: true);

        var d = TransferMatcher.Match([debit, plain, hinted]);

        For(d, debit).PartnerId.Should().Be(hinted.TransactionId);
        For(d, hinted).PartnerId.Should().Be(debit.TransactionId);
        For(d, hinted).Kind.Should().Be(TransactionKind.Transfer);
        For(d, plain).Kind.Should().Be(TransactionKind.PendingTransfer);
        For(d, plain).PartnerId.Should().BeNull();
    }

    [Fact]
    public void Match_SameAccount_NotPaired()
    {
        var debit = C(AccA, AccountOwner.Personal, 5, 100m, TransactionType.Debit);
        var credit = C(AccA, AccountOwner.Personal, 5, 100m, TransactionType.Credit);

        TransferMatcher.Match([debit, credit]).Should().OnlyContain(x => x.Kind == TransactionKind.Regular);
    }

    [Fact]
    public void Match_UnpairedWithHint_IsPending()
    {
        var debit = C(AccA, AccountOwner.Personal, 5, 100m, TransactionType.Debit, hint: true);

        var d = TransferMatcher.Match([debit]);

        d.Should().ContainSingle().Which.Kind.Should().Be(TransactionKind.PendingTransfer);
    }

    [Fact]
    public void Match_EachTransactionPairedOnce()
    {
        var debit = C(AccA, AccountOwner.Personal, 5, 100m, TransactionType.Debit);
        var credits = new[]
        {
            C(AccB, AccountOwner.Personal, 5, 100m, TransactionType.Credit),
            C(AccB, AccountOwner.Personal, 5, 100m, TransactionType.Credit),
            C(AccC, AccountOwner.Personal, 5, 100m, TransactionType.Credit),
        };

        var d = TransferMatcher.Match([debit, .. credits]);

        d.Should().HaveCount(4);
        d.Should().OnlyContain(x => x.Kind == TransactionKind.PendingTransfer && x.PartnerId == null);
    }

    [Fact]
    public void Match_ShuffledPool_GivesIdenticalDecisions()
    {
        var debit = C(AccA, AccountOwner.Personal, 5, 100m, TransactionType.Debit);
        var plain = C(AccB, AccountOwner.Personal, 5, 100m, TransactionType.Credit);
        var hinted = C(AccC, AccountOwner.Personal, 6, 100m, TransactionType.Credit, hint: true);
        var other1 = C(AccA, AccountOwner.Corporation, 10, 3000m, TransactionType.Debit);
        var other2 = C(AccB, AccountOwner.Personal, 11, 3000m, TransactionType.Credit);
        MatchCandidate[] pool = [debit, plain, hinted, other1, other2];

        var expected = TransferMatcher.Match(pool).OrderBy(x => x.TransactionId).ToList();
        var rng = new Random(42);
        for (var i = 0; i < 20; i++)
        {
            var shuffled = pool.OrderBy(_ => rng.Next()).ToList();
            TransferMatcher.Match(shuffled).OrderBy(x => x.TransactionId).Should().Equal(expected);
        }
    }

    [Theory]
    [InlineData("PAYMENT THANK YOU", true)]
    [InlineData("LOBLAWS", false)]
    [InlineData("E TRANSFER SENT", true)]
    [InlineData("WEALTHSIMPLE CASH", true)]
    public void HasHint_MatchesSpec(string d, bool expected) => TransferMatcher.HasHint(d).Should().Be(expected);
}
