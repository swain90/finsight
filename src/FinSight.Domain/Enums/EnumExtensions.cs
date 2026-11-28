namespace FinSight.Domain.Enums;

public static class AccountTypeExtensions
{
    /// <summary>True when the balance represents money owed.</summary>
    public static bool IsLiability(this AccountType type) =>
        type is AccountType.CreditCard or AccountType.LineOfCredit or AccountType.Mortgage or AccountType.Loan;

    public static bool IsInvestment(this AccountType type) =>
        type is AccountType.Investment or AccountType.Retirement or AccountType.Tfsa
            or AccountType.Rrsp or AccountType.Fhsa or AccountType.Crypto;

    public static bool IsRegistered(this AccountType type) =>
        type is AccountType.Tfsa or AccountType.Rrsp or AccountType.Fhsa;
}

public static class TransactionCategoryExtensions
{
    public static bool IsIncome(this TransactionCategory category) =>
        category is TransactionCategory.Salary or TransactionCategory.FreelanceIncome
            or TransactionCategory.InvestmentIncome or TransactionCategory.GovernmentBenefits
            or TransactionCategory.PayFromCorporation;
}
