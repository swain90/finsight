using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;

namespace FinSight.Application.Features.Imports.Common;

/// <summary>Builds an <see cref="ImportBatchDto"/> by loading the latest snapshot of each account shown on the review screen.</summary>
public static class ImportDtoLoader
{
    /// <param name="batch">The batch to map.</param>
    /// <param name="userAccounts">All of the user's accounts.</param>
    /// <param name="snapshotRepository">Source of latest balance snapshots.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The DTO exposing accounts assigned in the batch plus active accounts of the batch's institution.</returns>
    public static async Task<ImportBatchDto> BuildAsync(
        ImportBatch batch,
        IReadOnlyList<Account> userAccounts,
        IBalanceSnapshotRepository snapshotRepository,
        CancellationToken cancellationToken)
    {
        var assignedIds = batch.Rows.Where(r => r.AccountId is not null).Select(r => r.AccountId!.Value).ToHashSet();
        var shown = userAccounts
            .Where(a => assignedIds.Contains(a.Id) || (a.IsActive && a.Institution == batch.Institution))
            .ToList();
        var snapshots = new Dictionary<Guid, BalanceSnapshot?>();
        foreach (var account in shown)
            snapshots[account.Id] = await snapshotRepository.GetLatestAsync(account.Id, null, cancellationToken);
        return ImportDtoMapper.ToDto(batch, shown, snapshots);
    }
}
