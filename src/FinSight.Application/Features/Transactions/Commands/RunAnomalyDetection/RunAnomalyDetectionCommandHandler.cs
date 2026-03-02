using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FinSight.Application.Features.Transactions.Commands.RunAnomalyDetection;

public sealed class RunAnomalyDetectionCommandHandler(
    ITransactionRepository transactionRepository,
    IAnomalyDetectionService anomalyDetectionService,
    IUnitOfWork unitOfWork,
    ILogger<RunAnomalyDetectionCommandHandler> logger) : IRequestHandler<RunAnomalyDetectionCommand, Result>
{
    public async Task<Result> Handle(RunAnomalyDetectionCommand request, CancellationToken cancellationToken)
    {
        var from = DateTimeOffset.UtcNow.AddDays(-90);
        var transactions = await transactionRepository.GetByUserIdAsync(
            request.UserId, from, DateTimeOffset.UtcNow, cancellationToken);

        if (transactions.Count < 5)
            return Result.Success();

        var anomalousIds = await anomalyDetectionService.DetectAnomaliesAsync(transactions, cancellationToken);
        var anomalousSet = anomalousIds.ToHashSet();
        int marked = 0;

        foreach (var tx in transactions.Where(t => anomalousSet.Contains(t.Id) && !t.IsAnomaly))
        {
            tx.MarkAsAnomaly();
            marked++;
        }

        if (marked > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation("User {UserId}: marked {Count} new anomalies.", request.UserId, marked);
        }

        return Result.Success();
    }
}
