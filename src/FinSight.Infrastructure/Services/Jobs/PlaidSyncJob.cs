using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Application.Features.Transactions.Commands.RunAnomalyDetection;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Domain.Repositories;
using FinSight.Domain.ValueObjects;
using Hangfire;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FinSight.Infrastructure.Services.Jobs;

/// <summary>
/// Hangfire background job that syncs Plaid transactions for all linked accounts.
///
/// Flow per user:
///   1. Find all accounts with a PlaidAccountId
///   2. Fetch transactions from Plaid for the past 30 days
///   3. Skip transactions already imported (idempotent via PlaidTransactionId)
///   4. Categorize new transactions with ML.NET
///   5. Update account balances from Plaid's authoritative balance
///   6. Run anomaly detection across the updated dataset
///   7. Save everything in one SaveChanges call
/// </summary>
[AutomaticRetry(Attempts = 3, DelaysInSeconds = [60, 300, 900])]
public sealed class PlaidSyncJob(
    IAccountRepository accountRepository,
    ITransactionRepository transactionRepository,
    IUserRepository userRepository,
    IPlaidService plaidService,
    ITransactionCategorizationService categorizationService,
    IMediator mediator,
    IUnitOfWork unitOfWork,
    ILogger<PlaidSyncJob> logger)
{
    // Entry point called by Hangfire scheduler — runs every hour
    public async Task SyncAllUsersAsync(CancellationToken cancellationToken = default)
    {
        // In a real system you'd page through users; simplified here for clarity
        // Could also be enqueued per-user from a parent job
        logger.LogInformation("Plaid sync started for all users.");
        // Enqueue individual jobs per user (fan-out pattern)
        // BackgroundJob.Enqueue(() => SyncUserAsync(userId, CancellationToken.None));
    }

    /// <summary>Syncs a single user's Plaid-linked accounts. Safe to retry.</summary>
    public async Task SyncUserAsync(Guid userId, string plaidAccessToken, CancellationToken cancellationToken = default)
    {
        using var _ = logger.BeginScope(new { UserId = userId });
        logger.LogInformation("Starting Plaid sync for user {UserId}", userId);

        var accounts = await accountRepository.GetByUserIdAsync(userId, cancellationToken);
        var plaidLinkedAccounts = accounts.Where(a => a.PlaidAccountId is not null).ToList();

        if (plaidLinkedAccounts.Count == 0)
        {
            logger.LogInformation("User {UserId} has no Plaid-linked accounts — skipping.", userId);
            return;
        }

        // Step 1: Fetch latest balances and transactions from Plaid
        var endDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var startDate = endDate.AddDays(-30);

        var plaidAccounts = await plaidService.GetAccountsAsync(plaidAccessToken, cancellationToken);
        var plaidTransactions = await plaidService.GetTransactionsAsync(plaidAccessToken, startDate, endDate, cancellationToken);

        int imported = 0;
        int balancesUpdated = 0;

        foreach (var account in plaidLinkedAccounts)
        {
            // Step 2: Update balance from Plaid
            var plaidAccount = plaidAccounts.FirstOrDefault(pa => pa.PlaidAccountId == account.PlaidAccountId);
            if (plaidAccount is not null)
            {
                account.UpdateBalance(new Money(plaidAccount.Balance, plaidAccount.Currency));
                balancesUpdated++;
            }

            // Step 3: Import new transactions (idempotent)
            var accountTransactions = plaidTransactions
                .Where(t => t.AccountId == account.PlaidAccountId)
                .ToList();

            foreach (var plaidTx in accountTransactions)
            {
                // Skip if already imported
                if (await IsAlreadyImported(plaidTx.PlaidTransactionId, cancellationToken))
                    continue;

                var txType = plaidTx.Type == "Credit" ? TransactionType.Credit : TransactionType.Debit;
                var amount = new Money(Math.Abs(plaidTx.Amount), "CAD");

                // Step 4: ML.NET categorization
                var category = categorizationService.Categorize(
                    plaidTx.MerchantName ?? string.Empty,
                    plaidTx.Description);

                var transaction = account.RecordTransaction(
                    plaidTx.Description,
                    amount,
                    txType,
                    new DateTimeOffset(plaidTx.Date, TimeSpan.Zero),
                    category,
                    plaidTx.MerchantName);

                transaction.LinkToPlaid(plaidTx.PlaidTransactionId);
                imported++;
            }

            await accountRepository.UpdateAsync(account, cancellationToken);
        }

        // Step 5: Persist all changes
        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Plaid sync complete for user {UserId}. Imported: {Imported}, Balances updated: {Balances}",
            userId, imported, balancesUpdated);

        // Step 6: Run anomaly detection if new transactions were imported
        if (imported > 0)
        {
            await mediator.Send(new RunAnomalyDetectionCommand(userId), cancellationToken);
        }
    }

    private async Task<bool> IsAlreadyImported(string plaidTransactionId, CancellationToken ct)
    {
        // The transaction repository checks by PlaidTransactionId unique index
        // We rely on the unique DB constraint as a safety net, but check here first to avoid exceptions
        var existing = await transactionRepository.GetByPlaidIdAsync(plaidTransactionId, ct);
        return existing is not null;
    }
}
