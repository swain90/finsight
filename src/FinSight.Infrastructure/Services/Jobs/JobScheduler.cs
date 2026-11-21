using Hangfire;
using Microsoft.Extensions.Logging;

namespace FinSight.Infrastructure.Services.Jobs;

/// <summary>
/// Registers recurring Hangfire jobs on application startup.
/// All schedules use UTC cron expressions.
/// </summary>
public static class JobScheduler
{
    public static void RegisterRecurringJobs(IRecurringJobManager recurringJobs, ILogger logger)
    {
        // Plaid sync: every hour
        // Use the DI-resolved manager: the static RecurringJob API requires JobStorage.Current,
        // which is only initialised once Hangfire's services have been resolved.
        recurringJobs.AddOrUpdate<PlaidSyncJob>(
            recurringJobId: "plaid-sync-all-users",
            methodCall: job => job.SyncAllUsersAsync(CancellationToken.None),
            cronExpression: Cron.Hourly(),
            options: new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

        logger.LogInformation("Registered recurring Hangfire jobs.");
    }
}
