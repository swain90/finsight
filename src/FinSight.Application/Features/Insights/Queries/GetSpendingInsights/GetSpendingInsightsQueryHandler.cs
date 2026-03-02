using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Insights.Queries.GetSpendingInsights;

public sealed class GetSpendingInsightsQueryHandler(
    ITransactionRepository transactionRepository,
    ICurrentUserService currentUserService,
    IAiInsightsService aiInsightsService) : IRequestHandler<GetSpendingInsightsQuery, Result<SpendingInsightsDto>>
{
    public async Task<Result<SpendingInsightsDto>> Handle(GetSpendingInsightsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUserService.IsAuthenticated || currentUserService.UserId is null)
            return Result.Failure<SpendingInsightsDto>(Error.Unauthorized);

        var from = new DateTimeOffset(request.Year, request.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var to = from.AddMonths(1).AddTicks(-1);

        var transactions = await transactionRepository.GetByUserIdAsync(
            currentUserService.UserId.Value, from, to, cancellationToken);

        var debits = transactions.Where(t => t.Type == TransactionType.Debit).ToList();
        var credits = transactions.Where(t => t.Type == TransactionType.Credit).ToList();

        var totalSpent = debits.Sum(t => t.Amount.Amount);
        var totalIncome = credits.Sum(t => t.Amount.Amount);
        var currency = transactions.FirstOrDefault()?.Amount.Currency ?? "USD";

        // Category breakdown
        var breakdown = debits
            .GroupBy(t => t.Category)
            .Select(g => new CategoryBreakdownDto(
                g.Key.ToString(),
                g.Sum(t => t.Amount.Amount),
                g.Count(),
                totalSpent > 0 ? Math.Round(g.Sum(t => t.Amount.Amount) / totalSpent * 100, 1) : 0
            ))
            .OrderByDescending(c => c.Amount)
            .ToList()
            .AsReadOnly();

        var anomalyCount = transactions.Count(t => t.IsAnomaly);

        // Build summary for AI
        var summary = $"""
            Month: {request.Month}/{request.Year}
            Total Spent: {totalSpent:C} {currency}
            Total Income: {totalIncome:C} {currency}
            Net Flow: {totalIncome - totalSpent:C}
            Top Categories: {string.Join(", ", breakdown.Take(3).Select(c => $"{c.Category}: {c.Amount:C}"))}
            Anomalies Detected: {anomalyCount}
            """;

        var aiInsight = await aiInsightsService.GenerateSpendingInsightAsync(summary, cancellationToken);
        var insights = new List<string> { aiInsight }.AsReadOnly();

        return Result.Success(new SpendingInsightsDto(
            request.Month, request.Year,
            totalSpent, totalIncome, totalIncome - totalSpent,
            currency, breakdown, insights, anomalyCount));
    }
}
