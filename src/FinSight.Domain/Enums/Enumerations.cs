// =============================================================================
// Enumerations.cs — All domain enumerations for FinSight
// =============================================================================
//
// These enums represent the core domain vocabulary for the personal finance domain.
//
// WHY ENUMS IN THE DOMAIN LAYER?
//   These enums ARE part of the domain model — they express the bounded context's
//   ubiquitous language. "A transaction is either a Debit or a Credit" is pure
//   domain knowledge, not infrastructure or application concern.
//
// STORAGE: EF Core stores these as integers by default. For better readability
//   in the database and easier migrations when enum values change, consider using
//   string storage: property.HasConversion<string>()
//   This is configured in the entity type configurations in the Infrastructure layer.
// =============================================================================

namespace FinSight.Domain.Enums;

// =============================================================================
// TRANSACTION ENUMERATIONS
// =============================================================================

/// <summary>
/// Represents the direction of money flow for a financial transaction.
/// This is fundamental to double-entry bookkeeping principles.
/// </summary>
public enum TransactionType
{
    /// <summary>
    /// Money flowing OUT of an account (purchase, withdrawal, payment).
    /// Decreases the account balance.
    /// </summary>
    Debit = 1,

    /// <summary>
    /// Money flowing INTO an account (deposit, refund, income).
    /// Increases the account balance.
    /// </summary>
    Credit = 2
}

/// <summary>
/// Spending/income categories for transaction classification.
/// Used by both manual categorization and the ML.NET auto-categorizer.
/// 
/// These categories align with common personal finance frameworks
/// (Mint, YNAB, Personal Capital) for user familiarity.
/// </summary>
public enum TransactionKind
{
    Regular = 0,
    Transfer = 1,
    OwnerPay = 2,
    InvestmentTrade = 3,
    PendingTransfer = 4
}

public enum TransactionCategory
{
    /// <summary>Not yet categorized — triggers ML categorization job.</summary>
    Uncategorized = 0,

    // ---- ESSENTIAL EXPENSES ------------------------------------------------
    /// <summary>Mortgage/rent payments, property taxes, HOA fees.</summary>
    HousingAndRent = 10,
    /// <summary>Electricity, gas, water, internet, phone bills.</summary>
    Utilities = 11,
    /// <summary>Groceries, household supplies. NOT restaurants.</summary>
    GroceriesAndFood = 12,
    /// <summary>Gas, car insurance, parking, public transit, rideshare.</summary>
    Transportation = 13,
    /// <summary>Health insurance, doctor visits, prescriptions, gym membership.</summary>
    HealthAndMedical = 14,

    // ---- LIFESTYLE EXPENSES ------------------------------------------------
    /// <summary>Restaurants, bars, food delivery, coffee shops.</summary>
    DiningOut = 20,
    /// <summary>Streaming services, movies, concerts, sports events.</summary>
    Entertainment = 21,
    /// <summary>Clothing, shoes, accessories.</summary>
    ShoppingAndClothing = 22,
    /// <summary>Personal care: haircuts, cosmetics, spa.</summary>
    PersonalCare = 23,
    /// <summary>Vacations, hotels, flights, Airbnb.</summary>
    TravelAndVacation = 24,

    // ---- FINANCIAL --------------------------------------------------------
    /// <summary>Credit card payments, loan payments, student loans.</summary>
    DebtPayments = 30,
    /// <summary>Retirement contributions, brokerage investments, crypto.</summary>
    Savings = 31,
    /// <summary>Life insurance, disability insurance (not health).</summary>
    Insurance = 32,
    /// <summary>Bank fees, ATM fees, wire transfer fees.</summary>
    BankFees = 33,

    // ---- INCOME -----------------------------------------------------------
    /// <summary>Regular employment income, direct deposits.</summary>
    Salary = 40,
    /// <summary>Freelance work, gig economy income.</summary>
    FreelanceIncome = 41,
    /// <summary>Dividends, interest, capital gains.</summary>
    InvestmentIncome = 42,
    /// <summary>Tax refunds, government benefits.</summary>
    GovernmentBenefits = 43,
    /// <summary>Salary or dividends paid to the owner by their corporation.</summary>
    PayFromCorporation = 44,

    // ---- OTHER ------------------------------------------------------------
    /// <summary>Education: tuition, books, courses, software tools.</summary>
    EducationAndWork = 50,
    /// <summary>Charitable donations, gifts given.</summary>
    CharitableGiving = 51,
    /// <summary>Pet food, vet visits, pet insurance.</summary>
    PetsAndAnimals = 52,
    /// <summary>Everything else that doesn't fit above categories.</summary>
    Other = 99
}

// =============================================================================
// ACCOUNT ENUMERATIONS
// =============================================================================

/// <summary>
/// The type of financial account being tracked.
/// Determines calculation logic (e.g., credit cards contribute negatively to net worth).
/// </summary>
public enum AccountType
{
    /// <summary>Standard bank checking account. Positive balance = asset.</summary>
    Checking = 1,

    /// <summary>Savings account. Positive balance = asset.</summary>
    Savings = 2,

    /// <summary>
    /// Credit card account. Balance represents AMOUNT OWED (liability).
    /// When calculating net worth: contributes NEGATIVELY.
    /// </summary>
    CreditCard = 3,

    /// <summary>
    /// Investment/brokerage account. Positive balance = asset.
    /// Balance may fluctuate with market values.
    /// </summary>
    Investment = 4,

    /// <summary>Individual retirement account (IRA, 401k, RRSP, etc.).</summary>
    Retirement = 5,

    /// <summary>Mortgage or home equity loan. Balance = remaining principal owed.</summary>
    Mortgage = 6,

    /// <summary>Student loan, auto loan, personal loan.</summary>
    Loan = 7,

    /// <summary>Physical cash on hand tracked manually.</summary>
    Cash = 8,

    /// <summary>Line of credit. Balance = amount owed (liability).</summary>
    LineOfCredit = 9,

    /// <summary>Tax-Free Savings Account (Canada).</summary>
    Tfsa = 10,

    /// <summary>Registered Retirement Savings Plan (Canada).</summary>
    Rrsp = 11,

    /// <summary>First Home Savings Account (Canada).</summary>
    Fhsa = 12,

    /// <summary>Cryptocurrency holdings.</summary>
    Crypto = 13
}

/// <summary>Who owns an account: the individual or their corporation.</summary>
public enum AccountOwner
{
    Personal = 0,
    Corporation = 1
}

/// <summary>Financial institution an account is held at.</summary>
public enum Institution
{
    Other = 0,
    Cibc = 1,
    Td = 2,
    Mbna = 3,
    Rbc = 4,
    Wealthsimple = 5
}

/// <summary>
/// Connection status of a linked bank account.
/// Determines whether automated transaction sync is available.
/// </summary>
public enum AccountConnectionStatus
{
    /// <summary>Account was added manually — no bank link, no auto-sync.</summary>
    Manual = 0,

    /// <summary>Successfully linked via Plaid — auto-sync is active.</summary>
    Connected = 1,

    /// <summary>Bank link requires user to re-authenticate (expired session, password change).</summary>
    RequiresReauthentication = 2,

    /// <summary>Bank returned an error — contact support or re-link.</summary>
    Error = 3
}

// =============================================================================
// BUDGET ENUMERATIONS
// =============================================================================

/// <summary>
/// The period over which a budget resets.
/// </summary>
public enum BudgetPeriod
{
    /// <summary>Budget resets every week (Monday–Sunday).</summary>
    Weekly = 1,

    /// <summary>Budget resets every calendar month.</summary>
    Monthly = 2,

    /// <summary>Budget covers a 3-month quarter.</summary>
    Quarterly = 3,

    /// <summary>Budget covers an entire calendar year.</summary>
    Yearly = 4
}

/// <summary>
/// Alert threshold for budget notifications.
/// When spending reaches this percentage of the budget, an alert is triggered.
/// </summary>
public enum BudgetAlertThreshold
{
    /// <summary>Alert when 50% of budget is spent.</summary>
    HalfSpent = 50,

    /// <summary>Alert when 75% of budget is spent.</summary>
    ThreeQuartersSpent = 75,

    /// <summary>Alert when 90% of budget is spent.</summary>
    NinetyPercent = 90,

    /// <summary>Alert when budget is fully spent (100%).</summary>
    FullySpent = 100
}
