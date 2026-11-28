using System.Security.Cryptography;
using System.Text;
using FinSight.Domain.Enums;

namespace FinSight.Application.Features.Imports.Common;

/// <summary>
/// Identifies an imported statement row so re-imports of the same file can be detected.
/// </summary>
public static class ImportFingerprint
{
    /// <summary>
    /// Computes the SHA-256 fingerprint of a row, as lowercase hex (64 characters).
    /// The amount is formatted with the invariant culture so the result does not depend on the host culture.
    /// </summary>
    /// <param name="accountId">Account the row belongs to.</param>
    /// <param name="date">Statement date of the row.</param>
    /// <param name="amount">Row amount.</param>
    /// <param name="type">Transaction direction.</param>
    /// <param name="normalizedDescription">Output of <see cref="DescriptionNormalizer.Normalize"/>.</param>
    /// <param name="occurrence">Zero-based index among earlier identical rows in the same file.</param>
    /// <returns>The lowercase hex fingerprint.</returns>
    public static string Compute(
        Guid accountId,
        DateOnly date,
        decimal amount,
        TransactionType type,
        string normalizedDescription,
        int occurrence)
    {
        var key = FormattableString.Invariant(
            $"{accountId:N}|{date:yyyy-MM-dd}|{amount:0.00}|{type}|{normalizedDescription}|{occurrence}");

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(key));

        return Convert.ToHexString(hash).ToLowerInvariant()[..64];
    }

    /// <summary>
    /// For each row, returns how many earlier rows in the same file have identical
    /// date, amount, type and normalized description (zero-based).
    /// </summary>
    /// <param name="rows">Rows of one file, in file order.</param>
    /// <returns>One occurrence index per row, in the same order.</returns>
    public static IReadOnlyList<int> OccurrenceIndexes(
        IReadOnlyList<(DateOnly Date, decimal Amount, TransactionType Type, string NormalizedDescription)> rows)
    {
        var seen = new Dictionary<(DateOnly Date, decimal Amount, TransactionType Type, string NormalizedDescription), int>();
        var indexes = new List<int>(rows.Count);

        foreach (var row in rows)
        {
            var key = (row.Date, row.Amount, row.Type, row.NormalizedDescription);
            var occurrence = seen.GetValueOrDefault(key);

            indexes.Add(occurrence);
            seen[key] = occurrence + 1;
        }

        return indexes;
    }
}
