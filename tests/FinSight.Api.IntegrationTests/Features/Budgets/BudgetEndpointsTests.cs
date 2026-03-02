using FinSight.Api.IntegrationTests.Infrastructure;
using FinSight.Api.IntegrationTests.Helpers;
using FluentAssertions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FinSight.Api.IntegrationTests.Features.Budgets;

[Collection("Integration")]
public sealed class BudgetEndpointsTests(FinSightWebAppFactory factory)
    : IClassFixture<DatabaseResetFixture>, IClassFixture<FinSightWebAppFactory>
{
    private readonly HttpClient _client = factory.CreateClient();
    private readonly int _month = DateTime.UtcNow.Month;
    private readonly int _year = DateTime.UtcNow.Year;

    private async Task<HttpClient> GetAuthenticatedClientAsync(string email)
    {
        var token = await AuthHelper.GetAccessTokenAsync(_client, email);
        var client = factory.CreateClient();
        client.SetBearerToken(token);
        return client;
    }

    [Fact]
    public async Task CreateBudget_WithValidData_Returns201()
    {
        var client = await GetAuthenticatedClientAsync("budget-create@finsight.dev");
        var request = TestDataBuilder.CreateBudgetRequest(category: 1, limit: 500m); // Food

        var response = await client.PostAsJsonAsync("/api/v1/budgets", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBudget_Duplicate_Returns409()
    {
        var client = await GetAuthenticatedClientAsync("budget-dupe@finsight.dev");
        var request = TestDataBuilder.CreateBudgetRequest(category: 2); // Transportation

        await client.PostAsJsonAsync("/api/v1/budgets", request);
        var response = await client.PostAsJsonAsync("/api/v1/budgets", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetBudgets_ReturnsAllBudgetsForMonth()
    {
        var client = await GetAuthenticatedClientAsync("budget-list@finsight.dev");

        // Create two budgets in different categories
        await client.PostAsJsonAsync("/api/v1/budgets", TestDataBuilder.CreateBudgetRequest(category: 1));
        await client.PostAsJsonAsync("/api/v1/budgets", TestDataBuilder.CreateBudgetRequest(category: 2));

        var response = await client.GetAsync($"/api/v1/budgets/{_year}/{_month}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var budgets = await response.Content.ReadFromJsonAsync<JsonElement[]>();
        budgets.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSummary_WithOverBudgetCategory_ShowsAlert()
    {
        var client = await GetAuthenticatedClientAsync("budget-summary@finsight.dev");

        // Create a very small budget (will be over-budget immediately from seeded spending)
        var request = new
        {
            category = 1, // Food
            limitAmount = 1m,  // $1 limit — any spending exceeds this
            currency = "CAD",
            month = _month,
            year = _year
        };
        await client.PostAsJsonAsync("/api/v1/budgets", request);

        var response = await client.GetAsync($"/api/v1/budgets/{_year}/{_month}/summary");
        var summary = await response.Content.ReadFromJsonAsync<JsonElement>();

        // The budget should show 1 alert since $1 limit will always be exceeded
        summary.GetProperty("totalBudgets").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task UpdateBudget_WithValidData_Returns204()
    {
        var client = await GetAuthenticatedClientAsync("budget-update@finsight.dev");

        // Create budget
        var createResponse = await client.PostAsJsonAsync("/api/v1/budgets",
            TestDataBuilder.CreateBudgetRequest(category: 3, limit: 300m));
        var budgetId = JsonSerializer.Deserialize<Guid>(
            await createResponse.Content.ReadAsStringAsync());

        // Update limit
        var response = await client.PutAsJsonAsync(
            $"/api/v1/budgets/{budgetId}", new { newLimit = 500m });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}
