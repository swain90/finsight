using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
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

    [Fact]
    public async Task RecordTransaction_HousingAndRentDebit_CountsInHousingAndRentBudgetAndList()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var accountId = await CreateAccountAsync(client, "Rent Chequing");
        var now = DateTimeOffset.UtcNow;
        (await client.PostAsJsonAsync("/api/v1/budgets", new
        {
            Category = (int)TransactionCategory.HousingAndRent,
            LimitAmount = 2000m, Currency = "CAD", Month = now.Month, Year = now.Year
        })).EnsureSuccessStatusCode();

        (await client.PostAsJsonAsync("/api/v1/transactions", new
        {
            AccountId = accountId,
            Description = "Rent",
            Amount = 1500m,
            Currency = "CAD",
            Type = (int)TransactionType.Debit,
            OccurredAt = now,
            Category = (int)TransactionCategory.HousingAndRent
        })).StatusCode.Should().Be(HttpStatusCode.Created);

        var budgets = await client.GetFromJsonAsync<JsonElement>($"/api/v1/budgets/{now.Year}/{now.Month}");
        var budget = budgets.EnumerateArray().Single();
        budget.GetProperty("category").GetString().Should().Be("HousingAndRent");
        budget.GetProperty("spentAmount").GetDecimal().Should().Be(1500m);

        var txs = await client.GetFromJsonAsync<JsonElement>($"/api/v1/transactions/account/{accountId}?page=1&pageSize=10");
        txs.EnumerateArray().Single().GetProperty("category").GetString().Should().Be("HousingAndRent");
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = name,
            Type = (int)AccountType.Checking,
            Currency = "CAD"
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static async Task ImportAsync(HttpClient client, Guid accountId, string csv)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new("text/csv");
        form.Add(file, "file", "stmt.csv");
        form.Add(new StringContent(accountId.ToString()), "accountId");
        var created = await client.PostAsync("/api/v1/imports", form);
        created.EnsureSuccessStatusCode();
        var id = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        var commit = await client.PostAsJsonAsync($"/api/v1/imports/{id}/commit", new { balances = new[] { new { accountId, closingBalance = 1000m, asOf = "2026-02-11" } } });
        commit.EnsureSuccessStatusCode();
    }

    /// <summary>A 333.33 debit in one account with two equal credits elsewhere: ambiguous, so left pending.</summary>
    private static async Task<(Guid DebitAccount, Guid CreditAccountA, Guid CreditAccountB)> SeedAmbiguousTransferAsync(HttpClient client)
    {
        var from = await CreateAccountAsync(client, "Chequing");
        var toA = await CreateAccountAsync(client, "Savings A");
        var toB = await CreateAccountAsync(client, "Savings B");
        await ImportAsync(client, toA, "Date,Description,Amount\n2026-02-10,Deposit one,333.33\n");
        await ImportAsync(client, toB, "Date,Description,Amount\n2026-02-11,Deposit two,333.33\n");
        // Committed last, so it meets both credits at once and cannot be paired uniquely.
        await ImportAsync(client, from, "Date,Description,Amount\n2026-02-10,Moving money,-333.33\n");
        return (from, toA, toB);
    }

    [Fact]
    public async Task NeedsAttention_ThenResolveWithPartner_PairsAndClearsList()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        await SeedAmbiguousTransferAsync(client);

        var list = await client.GetFromJsonAsync<JsonElement>("/api/v1/transactions/needs-attention");
        var items = list.EnumerateArray().ToList();
        items.Should().HaveCount(3); // each leg is pending while ambiguous
        var debit = items.Single(i => i.GetProperty("type").GetString() == "Debit");
        debit.GetProperty("description").GetString().Should().Be("Moving money");
        debit.GetProperty("candidates").GetArrayLength().Should().Be(2);
        var partnerId = debit.GetProperty("candidates")[0].GetProperty("transactionId").GetGuid();

        var resolve = await client.PostAsJsonAsync(
            $"/api/v1/transactions/{debit.GetProperty("transactionId").GetGuid()}/resolve-transfer", new { partnerId });
        resolve.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var after = await client.GetFromJsonAsync<JsonElement>("/api/v1/transactions/needs-attention");
        after.EnumerateArray().Select(i => i.GetProperty("transactionId").GetGuid())
            .Should().NotContain(new[] { debit.GetProperty("transactionId").GetGuid(), partnerId });

        var accountId = debit.GetProperty("accountId").GetGuid();
        var txs = await client.GetFromJsonAsync<JsonElement>($"/api/v1/transactions/account/{accountId}?page=1&pageSize=10");
        txs.EnumerateArray().Single().GetProperty("kind").GetString().Should().Be("Transfer");
    }

    [Fact]
    public async Task ResolveTransfer_OtherUsersTransaction_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var other = await factory.CreateAuthenticatedClientAsync();
        await SeedAmbiguousTransferAsync(owner);
        var list = await owner.GetFromJsonAsync<JsonElement>("/api/v1/transactions/needs-attention");
        var id = list.EnumerateArray().First().GetProperty("transactionId").GetGuid();

        var response = await other.PostAsJsonAsync($"/api/v1/transactions/{id}/resolve-transfer", new { partnerId = (Guid?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
