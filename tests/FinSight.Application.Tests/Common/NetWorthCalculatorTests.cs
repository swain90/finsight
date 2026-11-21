using FinSight.Application.Common.Reporting;
using FinSight.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinSight.Application.Tests.Common;

public sealed class NetWorthCalculatorTests
{
    private static AccountBalanceSeries Acct(decimal current, bool liability = false,
        AccountOwner owner = AccountOwner.Personal, params (DateOnly, decimal)[] snaps) =>
        new(Guid.NewGuid(), owner, liability, current, snaps);

    [Fact]
    public void Current_AssetsMinusLiabilities_PersonalExcludesCorp()
    {
        var accounts = new[]
        {
            Acct(5000m), Acct(1200m, liability: true), Acct(20000m),
            Acct(30000m, owner: AccountOwner.Corporation)
        };

        var (personal, including) = NetWorthCalculator.Current(accounts);

        personal.Should().Be(23800m);
        including.Should().Be(53800m);
    }

    [Fact]
    public void At_UsesLatestSnapshotOnOrBefore()
    {
        var a = Acct(0m, false, AccountOwner.Personal,
            (new DateOnly(2025, 1, 31), 100m), (new DateOnly(2025, 2, 28), 200m), (new DateOnly(2025, 3, 31), 300m));

        var result = NetWorthCalculator.At(new DateOnly(2025, 2, 28), [a]);

        result.Should().Be((200m, 200m));
    }

    [Fact]
    public void At_AccountWithoutSnapshot_Skipped()
    {
        var withSnap = Acct(0m, false, AccountOwner.Personal, (new DateOnly(2025, 1, 31), 100m));
        var without = Acct(999m);

        var result = NetWorthCalculator.At(new DateOnly(2025, 2, 28), [withSnap, without]);

        result.Should().Be((100m, 100m));
    }

    [Fact]
    public void At_NoSnapshots_ReturnsNull()
    {
        NetWorthCalculator.At(new DateOnly(2025, 2, 28), [Acct(10m)]).Should().BeNull();
    }

    [Fact]
    public void At_LiabilityAndCorp_Handled()
    {
        var d = new DateOnly(2025, 1, 31);
        var accounts = new[]
        {
            Acct(0m, false, AccountOwner.Personal, (d, 1000m)),
            Acct(0m, true, AccountOwner.Personal, (d, 400m)),
            Acct(0m, false, AccountOwner.Corporation, (d, 5000m))
        };

        NetWorthCalculator.At(d, accounts).Should().Be((600m, 5600m));
    }
}
