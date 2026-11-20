using System.Net;
using System.Net.Http.Json;
using FinSight.Api.IntegrationTests.Helpers;
using FinSight.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinSight.Api.IntegrationTests.Tests;

[Collection("Integration")]
public sealed class TransactionsEndpointTests(FinSightWebApplicationFactory factory)
    : IClassFixture<FinSightWebApplicationFactory>
{
    [Fact]
    public async Task RecordTransaction_OnOwnAccount_Returns201()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        // Create an account first
        var accountResp = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "Test Chequing",
            Type = (int)AccountType.Checking,
            Currency = "CAD"
        });
        var accountId = await accountResp.Content.ReadFromJsonAsync<Guid>();

        // Record a transaction
        var txResp = await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            AccountId = accountId,
            Description = "Salary deposit",
            Amount = 3500m,
            Currency = "CAD",
            Type = (int)TransactionType.Credit,
            OccurredAt = DateTimeOffset.UtcNow,
            Category = (int)TransactionCategory.Salary
        });

        txResp.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task GetTransactions_OnOwnAccount_ReturnsTransactions()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var accountResp = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "Paginated Account",
            Type = (int)AccountType.Checking,
            Currency = "USD"
        });
        var accountId = await accountResp.Content.ReadFromJsonAsync<Guid>();

        // Post 3 transactions
        for (var i = 0; i < 3; i++)
        {
            await client.PostAsJsonAsync("/api/v1/transactions", new
            {
                AccountId = accountId,
                Description = $"Transaction {i + 1}",
                Amount = 10m * (i + 1),
                Currency = "USD",
                Type = (int)TransactionType.Debit,
                OccurredAt = DateTimeOffset.UtcNow.AddHours(-i)
            });
        }

        var response = await client.GetAsync($"/api/v1/transactions/account/{accountId}?page=1&pageSize=10");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Transaction 1");
        body.Should().Contain("Transaction 2");
        body.Should().Contain("Transaction 3");
    }

    [Fact]
    public async Task RecordTransaction_OnOtherUsersAccount_Returns401()
    {
        // User A creates account
        var clientA = await factory.CreateAuthenticatedClientAsync();
        var accountResp = await clientA.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "User A Account",
            Type = (int)AccountType.Checking,
            Currency = "USD"
        });
        var accountId = await accountResp.Content.ReadFromJsonAsync<Guid>();

        // User B attempts to post a transaction on User A's account (BOLA attack)
        var clientB = await factory.CreateAuthenticatedClientAsync();
        var response = await clientB.PostAsJsonAsync("/api/v1/transactions", new
        {
            AccountId = accountId,
            Description = "Unauthorized transaction",
            Amount = 1000m,
            Currency = "USD",
            Type = (int)TransactionType.Debit,
            OccurredAt = DateTimeOffset.UtcNow
        });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
