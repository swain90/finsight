using FinSight.Application.Common.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FinSight.Infrastructure.Services.AI;

public sealed class AiInsightsService(IChatClient chatClient, ILogger<AiInsightsService> logger) : IAiInsightsService
{
    private const string SystemPrompt = """
        You are FinSight, a personal finance AI assistant. 
        Provide concise, actionable, and encouraging financial insights.
        Be specific and data-driven. Keep responses under 3 sentences.
        Avoid generic advice — focus on the actual numbers provided.
        """;

    public async Task<string> GenerateSpendingInsightAsync(string spendingSummary, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = chatClient;
            _ = spendingSummary;
            await Task.CompletedTask;
            return "AI insight temporarily unavailable with current runtime package set.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate spending insight");
            return "AI insights are temporarily unavailable. Check your spending breakdown above for details.";
        }
    }

    public async Task<string> GenerateBudgetAdviceAsync(string budgetContext, CancellationToken cancellationToken = default)
    {
        try
        {
            _ = chatClient;
            _ = budgetContext;
            await Task.CompletedTask;
            return "AI budget advice temporarily unavailable with current runtime package set.";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate budget advice");
            return "Budget advice temporarily unavailable.";
        }
    }
}
