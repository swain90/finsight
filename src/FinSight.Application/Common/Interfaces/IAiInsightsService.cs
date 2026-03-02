namespace FinSight.Application.Common.Interfaces;

public interface IAiInsightsService
{
    Task<string> GenerateSpendingInsightAsync(string spendingSummary, CancellationToken cancellationToken = default);
    Task<string> GenerateBudgetAdviceAsync(string budgetContext, CancellationToken cancellationToken = default);
}
