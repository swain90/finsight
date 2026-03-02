using FinSight.Domain.Entities;

namespace FinSight.Application.Common.Interfaces;

public interface IAnomalyDetectionService
{
    Task<IReadOnlyList<Guid>> DetectAnomaliesAsync(IReadOnlyList<Transaction> transactions, CancellationToken cancellationToken = default);
}
