using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;

namespace FinSight.Application.Features.Imports.Common;

/// <summary>Computes fingerprints and duplicate flags per (batch, account) group, identically for create and edit.</summary>
public static class ImportRowFingerprinter
{
    /// <summary>Fingerprints one account's rows, given in order, and flags those already imported.</summary>
    /// <param name="accountId">The account the rows belong to.</param>
    /// <param name="rows">The account's rows in order (amount rounded to 2 dp).</param>
    /// <param name="transactionRepository">Source of already-imported fingerprints.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A fingerprint and duplicate flag per row, in the same order.</returns>
    public static async Task<IReadOnlyList<(string Fingerprint, bool IsDuplicate)>> ComputeAsync(
        Guid accountId,
        IReadOnlyList<(DateOnly Date, decimal Amount, TransactionType Type, string Normalized)> rows,
        ITransactionRepository transactionRepository,
        CancellationToken cancellationToken)
    {
        var occurrences = ImportFingerprint.OccurrenceIndexes(rows);
        var prints = rows
            .Select((r, i) => ImportFingerprint.Compute(accountId, r.Date, r.Amount, r.Type, r.Normalized, occurrences[i]))
            .ToList();
        var existing = await transactionRepository.GetExistingFingerprintsAsync(accountId, prints, cancellationToken);
        return prints.Select(p => (p, existing.Contains(p))).ToList();
    }

    /// <summary>
    /// Recomputes every row assigned to <paramref name="accountId"/> (plus <paramref name="joining"/>, which is
    /// assigned to it), so the result matches a fresh import regardless of assignment order.
    /// </summary>
    public static async Task RecomputeAccountAsync(
        IEnumerable<ImportRow> batchRows,
        Guid accountId,
        ImportRow? joining,
        ITransactionRepository transactionRepository,
        CancellationToken cancellationToken)
    {
        var group = batchRows
            .Where(r => r.AccountId == accountId && r.Id != joining?.Id)
            .Concat(joining is null ? [] : [joining])
            .OrderBy(r => r.LineNumber)
            .ToList();
        if (group.Count == 0) return;

        var inputs = group
            .Select(r => (r.OccurredOn, Math.Round(r.Amount.Amount, 2), r.Type, DescriptionNormalizer.Normalize(r.Description)))
            .ToList();
        var results = await ComputeAsync(accountId, inputs, transactionRepository, cancellationToken);

        for (var i = 0; i < group.Count; i++)
        {
            var row = group[i];
            var (fp, dup) = results[i];
            // Leave untouched rows alone so a manual include/exclude choice survives.
            if (ReferenceEquals(row, joining) || row.Fingerprint != fp || row.IsDuplicate != dup)
                row.AssignAccount(accountId, fp, dup);
        }
    }
}
