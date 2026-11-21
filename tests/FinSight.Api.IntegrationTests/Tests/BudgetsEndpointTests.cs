using System.Net;
using System.Net.Http.Json;
using FinSight.Api.IntegrationTests.Helpers;
using FinSight.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinSight.Api.IntegrationTests.Tests;

[Collection("Integration")]
public sealed class BudgetsEndpointTests(FinSightWebApplicationFactory factory)
    : IClassFixture<FinSightWebApplicationFactory>
{
    private static readonly int ThisMonth = DateTimeOffset.UtcNow.Month;
    private static readonly int ThisYear = DateTimeOffset.UtcNow.Year;

    [Fact]
    public async Task CreateBudget_WithValidData_Returns201()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PostAsJsonAsync("/api/v1/budgets", new
        {
            Category = (int)TransactionCategory.GroceriesAndFood,
            LimitAmount = 500m,
            Currency = "CAD",
            Month = ThisMonth,
            Year = ThisYear
        });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task CreateBudget_Duplicate_Returns409()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var payload = new
        {
            Category = (int)TransactionCategory.Entertainment,
            LimitAmount = 200m,
            Currency = "CAD",
            Month = ThisMonth,
            Year = ThisYear
        };

        await client.PostAsJsonAsync("/api/v1/budgets", payload);
        var second = await client.PostAsJsonAsync("/api/v1/budgets", payload);

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task GetBudgets_AfterCreating_ReturnsBudgetWithCorrectFields()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        await client.PostAsJsonAsync("/api/v1/budgets", new
        {
            Category = (int)TransactionCategory.Transportation,
            LimitAmount = 300m,
            Currency = "CAD",
            Month = ThisMonth,
            Year = ThisYear
        });

        var response = await client.GetAsync($"/api/v1/budgets/{ThisYear}/{ThisMonth}");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync();
        body.Should().Contain("Transportation");
        body.Should().Contain("300");
    }

    [Fact]
    public async Task GetBudgets_WithTwoBudgetsInMonth_ReturnsBoth()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        foreach (var category in new[] { TransactionCategory.Transportation, TransactionCategory.Entertainment })
        {
            await client.PostAsJsonAsync("/api/v1/budgets", new
            {
                Category = (int)category,
                LimitAmount = 100m, Currency = "CAD", Month = ThisMonth, Year = ThisYear
            });
        }

        var response = await client.GetAsync($"/api/v1/budgets/{ThisYear}/{ThisMonth}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var budgets = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement[]>();
        budgets.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetBudgetSummary_ReturnsAggregatedTotals()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        // Create 2 budgets
        await client.PostAsJsonAsync("/api/v1/budgets", new
        {
            Category = (int)TransactionCategory.HealthAndMedical,
            LimitAmount = 400m, Currency = "USD", Month = ThisMonth, Year = ThisYear
        });
        await client.PostAsJsonAsync("/api/v1/budgets", new
        {
            Category = (int)TransactionCategory.ShoppingAndClothing,
            LimitAmount = 600m, Currency = "USD", Month = ThisMonth, Year = ThisYear
        });

        var response = await client.GetAsync($"/api/v1/budgets/{ThisYear}/{ThisMonth}/summary");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        body.Should().ContainKey("totalBudgeted");
    }

    [Fact]
    public async Task UpdateBudget_WithNewAmount_Returns204()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        // Create budget
        var createResp = await client.PostAsJsonAsync("/api/v1/budgets", new
        {
            Category = (int)TransactionCategory.EducationAndWork,
            LimitAmount = 200m, Currency = "CAD", Month = ThisMonth, Year = ThisYear
        });
        var budgetId = await createResp.Content.ReadFromJsonAsync<Guid>();

        // Update it
        var updateResp = await client.PutAsJsonAsync($"/api/v1/budgets/{budgetId}", new
        {
            NewLimit = 350m
        });

        updateResp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteBudget_Returns204()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var createResp = await client.PostAsJsonAsync("/api/v1/budgets", new
        {
            Category = (int)TransactionCategory.TravelAndVacation,
            LimitAmount = 1000m, Currency = "USD", Month = ThisMonth, Year = ThisYear
        });
        var budgetId = await createResp.Content.ReadFromJsonAsync<Guid>();

        var deleteResp = await client.DeleteAsync($"/api/v1/budgets/{budgetId}");
        deleteResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        // Confirm deleted
        var getResp = await client.GetAsync($"/api/v1/budgets/{ThisYear}/{ThisMonth}");
        var body = await getResp.Content.ReadAsStringAsync();
        body.Should().NotContain(budgetId.ToString());
    }
}
