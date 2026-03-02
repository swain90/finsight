// =============================================================================
// Currency.cs — ISO 4217 Currency Codes
// =============================================================================
// Using an enum (not string) enforces compile-time safety.
// Adding a new currency requires a code change, not just a data change —
// intentional for a financial application where currency support
// has regulatory and conversion-rate implications.
// =============================================================================

namespace FinSight.Domain.Enums;

/// <summary>
/// ISO 4217 currency codes supported by FinSight.
/// Stored as int in the database for performance and index efficiency.
/// </summary>
public enum Currency
{
    USD = 1,   // United States Dollar
    EUR = 2,   // Euro
    GBP = 3,   // British Pound Sterling
    CAD = 4,   // Canadian Dollar
    AUD = 5,   // Australian Dollar
    JPY = 6,   // Japanese Yen
    CHF = 7,   // Swiss Franc
    BTC = 8,   // Bitcoin (crypto — note: 8 decimal places vs 2)
    ETH = 9,   // Ethereum
}
