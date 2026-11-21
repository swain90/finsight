using FinSight.Domain.Enums;

namespace FinSight.Application.Common.Reporting;

/// <summary>An account's current balance and snapshot history. Liabilities carry positive amounts owed.</summary>
public sealed record AccountBalanceSeries(
    Guid AccountId,
    AccountOwner Owner,
    bool IsLiability,
    decimal CurrentBalance,
    IReadOnlyList<(DateOnly AsOf, decimal Balance)> Snapshots);

/// <summary>Net worth = assets minus liabilities. "Personal" excludes Corporation-owned accounts.</summary>
public static class NetWorthCalculator
{
    public static (decimal Personal, decimal IncludingCorporation) Current(IReadOnlyList<AccountBalanceSeries> accounts) =>
        Sum(accounts.Select(a => (a, a.CurrentBalance)));

    /// <summary>
    /// Net worth using each account's latest snapshot on or before <paramref name="date"/>.
    /// Accounts with no such snapshot are skipped; null when no account has one.
    /// </summary>
    public static (decimal Personal, decimal IncludingCorporation)? At(DateOnly date, IReadOnlyList<AccountBalanceSeries> accounts)
    {
        var balances = new List<(AccountBalanceSeries, decimal)>();
        foreach (var account in accounts)
        {
            var latest = account.Snapshots.Where(s => s.AsOf <= date).OrderByDescending(s => s.AsOf).Cast<(DateOnly AsOf, decimal Balance)?>().FirstOrDefault();
            if (latest is not null)
                balances.Add((account, latest.Value.Balance));
        }
        return balances.Count == 0 ? null : Sum(balances);
    }

    private static (decimal Personal, decimal IncludingCorporation) Sum(IEnumerable<(AccountBalanceSeries Account, decimal Balance)> balances)
    {
        decimal personal = 0, all = 0;
        foreach (var (account, balance) in balances)
        {
            var signed = account.IsLiability ? -balance : balance;
            all += signed;
            if (account.Owner == AccountOwner.Personal)
                personal += signed;
        }
        return (personal, all);
    }
}
