using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace FinSight.Domain.Tests.Entities;

public class BalanceSnapshotTests
{
    private static Account Chequing() => Account.Create(Guid.NewGuid(), "Chequing", AccountType.Checking);

    [Fact]
    public void RecordBalanceSnapshot_Newer_UpdatesBalance()
    {
        var a = Chequing();
        var snap = a.RecordBalanceSnapshot(new DateOnly(2026, 10, 31), Money.CAD(500m), null);
        a.Balance.Amount.Should().Be(500m);
        a.LatestSnapshotAsOf.Should().Be(new DateOnly(2026, 10, 31));
        (snap.AccountId, snap.AsOf, snap.Balance.Amount).Should().Be((a.Id, new DateOnly(2026, 10, 31), 500m));
    }

    [Fact]
    public void RecordBalanceSnapshot_Older_KeepsNewerBalance()
    {
        var a = Chequing();
        a.RecordBalanceSnapshot(new DateOnly(2026, 10, 31), Money.CAD(500m), null);
        a.RecordBalanceSnapshot(new DateOnly(2026, 9, 30), Money.CAD(300m), null);
        a.Balance.Amount.Should().Be(500m);
        a.LatestSnapshotAsOf.Should().Be(new DateOnly(2026, 10, 31));
    }

    [Fact]
    public void ResetBalanceFromSnapshot_Null_ZeroesBalance()
    {
        var a = Chequing();
        a.RecordBalanceSnapshot(new DateOnly(2026, 10, 31), Money.CAD(500m), null);
        a.ResetBalanceFromSnapshot(null);
        a.Balance.Amount.Should().Be(0m);
        a.LatestSnapshotAsOf.Should().BeNull();
    }
}
