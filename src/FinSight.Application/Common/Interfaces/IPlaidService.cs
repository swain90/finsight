namespace FinSight.Application.Common.Interfaces;

public record PlaidAccount(string PlaidAccountId, string Name, string Type, decimal Balance, string Currency);
public record PlaidTransaction(string PlaidTransactionId, string AccountId, string Description, decimal Amount, string Type, DateTime Date, string? MerchantName, string? Category);

public interface IPlaidService
{
    Task<string> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlaidAccount>> GetAccountsAsync(string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PlaidTransaction>> GetTransactionsAsync(string accessToken, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default);
}
