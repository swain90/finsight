using FinSight.Api.IntegrationTests.Infrastructure;
using FinSight.Api.IntegrationTests.Helpers;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinSight.Api.IntegrationTests.Features.Accounts;

[Collection("Integration")]
public sealed class AccountEndpointsTests(FinSightWebAppFactory factory)
    : IClassFixture<DatabaseResetFixture>, IClassFixture<FinSightWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();

    private async Task<HttpClient> GetAuthenticatedClientAsync(string? email = null)
    {
        var token = await AuthHelper.GetAccessTokenAsync(_client, email ?? "accounts-test@finsight.dev");
        var client = factory.CreateClient();
        client.SetBearerToken(token);
        return client;
    }

    [Fact]
    public async Task GetAccounts_AuthenticatedUser_ReturnsEmptyListInitially()
    {
        var client = await GetAuthenticatedClientAsync("empty-accounts@finsight.dev");
        var response = await client.GetAsync("/api/v1/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        accounts.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAccount_WithValidData_Returns201AndId()
    {
        var client = await GetAuthenticatedClientAsync("create-account@finsight.dev");
        var request = TestDataBuilder.CreateAccountRequest("My Chequing", type: 1);

        var response = await client.PostAsJsonAsync("/api/v1/accounts", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await response.Content.ReadAsStringAsync();
        var id = JsonSerializer.Deserialize<Guid>(body);
        id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAccounts_AfterCreating_ReturnsCreatedAccount()
    {
        var client = await GetAuthenticatedClientAsync("list-accounts@finsight.dev");

        // Create an account
        await client.PostAsJsonAsync("/api/v1/accounts",
            TestDataBuilder.CreateAccountRequest("TD Savings", type: 2, currency: "CAD"));

        // Get accounts
        var response = await client.GetAsync("/api/v1/accounts");
        var accounts = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        accounts.Should().HaveCount(1);
        accounts![0].GetProperty("name").GetString().Should().Be("TD Savings");
        accounts![0].GetProperty("type").GetString().Should().Be("Savings");
    }

    [Fact]
    public async Task GetAccounts_OtherUsersAccounts_AreNotReturned()
    {
        // User A creates an account
        var clientA = await GetAuthenticatedClientAsync("user-a@finsight.dev");
        await clientA.PostAsJsonAsync("/api/v1/accounts",
            TestDataBuilder.CreateAccountRequest("User A's Account"));

        // User B should see zero accounts
        var clientB = await GetAuthenticatedClientAsync("user-b@finsight.dev");
        var response = await clientB.GetAsync("/api/v1/accounts");
        var accounts = await response.Content.ReadFromJsonAsync<JsonElement[]>();

        accounts.Should().BeEmpty("User B should not see User A's accounts — BOLA protection");
    }

    [Fact]
    public async Task CreateAccount_WithUnsupportedCurrency_Returns422()
    {
        var client = await GetAuthenticatedClientAsync("bad-currency@finsight.dev");
        var request = new { name = "Test", type = 1, currency = "XYZ" };

        var response = await client.PostAsJsonAsync("/api/v1/accounts", request);

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }
}
