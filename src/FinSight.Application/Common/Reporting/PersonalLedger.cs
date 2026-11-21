using FinSight.Domain.Entities;
using FinSight.Domain.Enums;

namespace FinSight.Application.Common.Reporting;

/// <summary>
/// Rules deciding which transactions count as real personal spending and income in reports.
/// Transfers, pay from the corporation, investment trades, pending transfers and anything on a
/// corporate account are not personal spending. Refunds reduce spending.
/// </summary>
public static class PersonalLedger
{
    public static bool IsPersonalSpending(Transaction t) =>
        t.Kind == TransactionKind.Regular
        && IsPersonalAccount(t)
        && !t.Category.IsIncome();

    /// <summary>Debit adds to spending; a credit (refund) reduces it.</summary>
    public static decimal SpendingAmount(Transaction t) =>
        t.Type == TransactionType.Debit ? t.Amount.Amount : -t.Amount.Amount;

    public static bool IsTaxableIncome(Transaction t) =>
        IsPersonalAccount(t)
        && t.Type == TransactionType.Credit
        && ((t.Kind == TransactionKind.Regular
                && t.Category.IsIncome()
                && !(t.Category == TransactionCategory.InvestmentIncome && t.Account.Type.IsRegistered()))
            || t.Kind == TransactionKind.OwnerPay);

    public static bool IsTaxShelteredIncome(Transaction t) =>
        IsPersonalAccount(t)
        && t.Kind == TransactionKind.Regular
        && t.Type == TransactionType.Credit
        && t.Category == TransactionCategory.InvestmentIncome
        && t.Account.Type.IsRegistered();

    private static bool IsPersonalAccount(Transaction t)
    {
        if (t.Account is null)
            throw new InvalidOperationException(
                $"Transaction {t.Id} has no loaded Account; load it with Include(t => t.Account) before applying personal ledger rules.");
        return t.Account.Owner == AccountOwner.Personal;
    }
}
