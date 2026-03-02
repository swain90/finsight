using System.Net.Http.Json;
using FinSight.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FinSight.Infrastructure.Services.Plaid;

public sealed class PlaidService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<PlaidService> logger) : IPlaidService
{
    private string ClientId => configuration["Plaid:ClientId"] ?? throw new InvalidOperationException("Plaid ClientId not configured.");
    private string Secret => configuration["Plaid:Secret"] ?? throw new InvalidOperationException("Plaid Secret not configured.");

    public async Task<string> ExchangePublicTokenAsync(string publicToken, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Plaid");
        var response = await client.PostAsJsonAsync("/item/public_token/exchange", new
        {
            client_id = ClientId,
            secret = Secret,
            public_token = publicToken
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PlaidTokenExchangeResponse>(cancellationToken: cancellationToken);
        return result?.AccessToken ?? throw new InvalidOperationException("Failed to exchange Plaid token.");
    }

    public async Task<IReadOnlyList<PlaidAccount>> GetAccountsAsync(string accessToken, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Plaid");
        var response = await client.PostAsJsonAsync("/accounts/get", new
        {
            client_id = ClientId,
            secret = Secret,
            access_token = accessToken
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PlaidAccountsResponse>(cancellationToken: cancellationToken);
        return result?.Accounts?.Select(a => new PlaidAccount(
            a.AccountId, a.Name, a.Type,
            a.Balances?.Current ?? 0,
            a.Balances?.IsoCurrencyCode ?? "USD")).ToList() ?? [];
    }

    public async Task<IReadOnlyList<PlaidTransaction>> GetTransactionsAsync(
        string accessToken, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        var client = httpClientFactory.CreateClient("Plaid");
        var response = await client.PostAsJsonAsync("/transactions/get", new
        {
            client_id = ClientId,
            secret = Secret,
            access_token = accessToken,
            start_date = startDate.ToString("yyyy-MM-dd"),
            end_date = endDate.ToString("yyyy-MM-dd")
        }, cancellationToken);

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<PlaidTransactionsResponse>(cancellationToken: cancellationToken);

        return result?.Transactions?.Select(t => new PlaidTransaction(
            t.TransactionId, t.AccountId, t.Name, t.Amount,
            t.Amount < 0 ? "Credit" : "Debit",
            DateTime.Parse(t.Date), t.MerchantName,
            t.PersonalFinanceCategory?.Primary)).ToList() ?? [];
    }

    // Response DTOs (internal to infrastructure)
    private sealed record PlaidTokenExchangeResponse(string AccessToken, string ItemId);
    private sealed record PlaidAccountsResponse(List<PlaidAccountData>? Accounts);
    private sealed record PlaidAccountData(string AccountId, string Name, string Type, PlaidBalances? Balances);
    private sealed record PlaidBalances(decimal? Current, string? IsoCurrencyCode);
    private sealed record PlaidTransactionsResponse(List<PlaidTransactionData>? Transactions);
    private sealed record PlaidTransactionData(string TransactionId, string AccountId, string Name, decimal Amount, string Date, string? MerchantName, PlaidCategory? PersonalFinanceCategory);
    private sealed record PlaidCategory(string Primary);
}
