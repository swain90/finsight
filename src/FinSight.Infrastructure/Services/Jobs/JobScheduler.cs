using Hangfire;
using Microsoft.Extensions.Logging;

namespace FinSight.Infrastructure.Services.Jobs;

/// <summary>
/// Registers recurring Hangfire jobs on application startup.
/// All schedules use UTC cron expressions.
/// </summary>
public static class JobScheduler
{
    public static void RegisterRecurringJobs(ILogger logger)
    {
        // Plaid sync: every hour
        RecurringJob.AddOrUpdate<PlaidSyncJob>(
            recurringJobId: "plaid-sync-all-users",
            methodCall: job => job.SyncAllUsersAsync(CancellationToken.None),
            cronExpression: Cron.Hourly(),
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        logger.LogInformation("Registered recurring Hangfire jobs.");
    }
}
