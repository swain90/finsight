using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Exceptions;

namespace FinSight.Domain.Entities;

public sealed class ImportBatch : BaseEntity
{
    private readonly List<ImportRow> _rows = [];

    public Guid UserId { get; private set; }
    public Institution Institution { get; private set; }
    public string FileName { get; private set; } = string.Empty;  // max 255
    public ImportBatchStatus Status { get; private set; } = ImportBatchStatus.PendingReview;
    public DateOnly? PeriodStart { get; private set; }
    public DateOnly? PeriodEnd { get; private set; }
    public DateTimeOffset? CommittedAt { get; private set; }
    public string? ReconciliationWarning { get; private set; }  // max 2000

    public IReadOnlyCollection<ImportRow> Rows => _rows.AsReadOnly();

    private ImportBatch() { }  // EF Core

    public static ImportBatch Create(Guid userId, Institution institution, string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var name = fileName.Trim();
        return new ImportBatch
        {
            UserId = userId,
            Institution = institution,
            FileName = name.Length > 255 ? name[..255] : name
        };
    }

    public void AddRow(ImportRow row)
    {
        _rows.Add(row);
        if (PeriodStart is null || row.OccurredOn < PeriodStart) PeriodStart = row.OccurredOn;
        if (PeriodEnd is null || row.OccurredOn > PeriodEnd) PeriodEnd = row.OccurredOn;
    }

    public void Commit(string? reconciliationWarning)
    {
        if (Status != ImportBatchStatus.PendingReview)
            throw new DomainException("Only a pending import batch can be committed.");

        Status = ImportBatchStatus.Committed;
        CommittedAt = DateTimeOffset.UtcNow;
        ReconciliationWarning = reconciliationWarning is { Length: > 2000 } ? reconciliationWarning[..2000] : reconciliationWarning;
        SetUpdatedAt();
    }

    public void Discard()
    {
        if (Status != ImportBatchStatus.PendingReview)
            throw new DomainException("Only a pending import batch can be discarded.");

        Status = ImportBatchStatus.Discarded;
        SetUpdatedAt();
    }

    public void MarkUndone()
    {
        if (Status != ImportBatchStatus.Committed)
            throw new DomainException("Only a committed import batch can be undone.");

        Status = ImportBatchStatus.Discarded;
        SetUpdatedAt();
    }
}
