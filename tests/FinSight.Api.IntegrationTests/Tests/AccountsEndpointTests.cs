using System.Net;
using System.Net.Http.Json;
using FinSight.Api.IntegrationTests.Helpers;
using FinSight.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinSight.Api.IntegrationTests.Tests;

[Collection("Integration")]
public sealed class AccountsEndpointTests(FinSightWebApplicationFactory factory)
    : IClassFixture<FinSightWebApplicationFactory>
{
    [Fact]
    public async Task GetAccounts_Authenticated_ReturnsEmptyList()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.GetAsync("/api/v1/accounts");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var accounts = await response.Content.ReadFromJsonAsync<List<object>>();
        accounts.Should().NotBeNull();
    }

    [Fact]
    public async Task CreateAccount_WithValidData_Returns201AndId()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "My Chequing",
            Type = (int)AccountType.Checking,
            Currency = "CAD",
            InstitutionName = "TD Bank"
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var id = await response.Content.ReadFromJsonAsync<Guid>();
        id.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CreateAccount_ThenGetAccounts_ReturnsCreatedAccount()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        // Create
        await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "Integration Test Account",
            Type = (int)AccountType.Savings,
            Currency = "USD"
        });

        // Read
        var response = await client.GetAsync("/api/v1/accounts");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().Contain("Integration Test Account");
    }

    [Fact]
    public async Task CreateAccount_InvalidCurrency_Returns422()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "Bad Account",
            Type = (int)AccountType.Checking,
            Currency = "XYZ"  // not in allowed list
        });

        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Accounts_AreUserScoped_OtherUserCannotSeeThem()
    {
        // User A creates an account
        var clientA = await factory.CreateAuthenticatedClientAsync();
        await clientA.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "User A Secret Account",
            Type = (int)AccountType.Savings,
            Currency = "USD"
        });

        // User B gets their accounts — should not see User A's account (BOLA protection)
        var clientB = await factory.CreateAuthenticatedClientAsync();
        var response = await clientB.GetAsync("/api/v1/accounts");
        var body = await response.Content.ReadAsStringAsync();

        body.Should().NotContain("User A Secret Account");
    }

    [Fact]
    public async Task CreateAccount_WithOwnerAndInstitution_RoundTrips()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var create = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "RBC Business Chequing",
            Type = (int)AccountType.Checking,
            Currency = "CAD",
            Institution = (int)Institution.Rbc,
            Owner = (int)AccountOwner.Corporation,
            ExternalAccountRef = "1234"
        });
        create.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await (await client.GetAsync("/api/v1/accounts")).Content.ReadAsStringAsync();
        body.Should().Contain("RBC Business Chequing");
        body.Should().Contain("\"institution\":\"Rbc\"");
        body.Should().Contain("\"owner\":\"Corporation\"");
        body.Should().Contain("\"externalAccountRef\":\"1234\"");
    }

    [Fact]
    public async Task UpdateAccount_Returns204()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var id = await (await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "Old Name",
            Type = (int)AccountType.Checking,
            Currency = "CAD"
        })).Content.ReadFromJsonAsync<Guid>();

        var response = await client.PutAsJsonAsync($"/api/v1/accounts/{id}", new
        {
            Name = "New Name",
            Type = (int)AccountType.Tfsa,
            Institution = (int)Institution.Wealthsimple,
            Owner = (int)AccountOwner.Personal,
            ExternalAccountRef = (string?)null
        });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var body = await (await client.GetAsync("/api/v1/accounts")).Content.ReadAsStringAsync();
        body.Should().Contain("New Name").And.Contain("Tfsa");
    }
}
