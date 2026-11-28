using System.Net;
using System.Net.Http.Json;
using FinSight.Api.IntegrationTests.Helpers;
using FinSight.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinSight.Api.IntegrationTests.Tests;

[Collection("Integration")]
public sealed class InsightsEndpointTests(FinSightWebApplicationFactory factory)
    : IClassFixture<FinSightWebApplicationFactory>
{
    [Fact]
    public async Task GetSpendingInsights_WithNoTransactions_ReturnsZeros()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var now = DateTimeOffset.UtcNow;

        var response = await client.GetAsync($"/api/v1/insights/spending/{now.Year}/{now.Month}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("totalSpent");
        body.Should().ContainKey("aiInsights");
    }

    [Fact]
    public async Task GetSpendingInsights_WithTransactions_ReturnsBreakdown()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        // Set up account and transactions
        var accountResp = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "Insights Test Account",
            Type = (int)AccountType.Checking,
            Currency = "USD"
        });
        var accountId = await accountResp.Content.ReadFromJsonAsync<Guid>();

        await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            AccountId = accountId,
            Description = "Grocery run",
            Amount = 150m,
            Currency = "USD",
            Type = (int)TransactionType.Debit,
            OccurredAt = DateTimeOffset.UtcNow,
            Category = (int)TransactionCategory.GroceriesAndFood,
            MerchantName = "Whole Foods"
        });

        var now = DateTimeOffset.UtcNow;
        var response = await client.GetAsync($"/api/v1/insights/spending/{now.Year}/{now.Month}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Food");
        body.Should().Contain("150");
    }

    [Fact]
    public async Task GetSpendingInsights_InvalidMonth_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var response = await client.GetAsync("/api/v1/insights/spending/2025/13");
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
