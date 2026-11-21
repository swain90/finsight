using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinSight.Api.IntegrationTests.Helpers;
using FinSight.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinSight.Api.IntegrationTests.Features.Imports;

/// <summary>
/// Spec §9 "upload → review → commit → dashboard totals": three statements (corporate chequing, personal
/// savings, personal card) committed over HTTP, then every total the dashboard and the tax estimate read.
/// </summary>
[Collection("Integration")]
public sealed class ImportTotalsEndToEndTests(FinSightWebApplicationFactory factory)
    : IClassFixture<FinSightWebApplicationFactory>
{
    private static async Task<JsonElement> JsonAsync(HttpResponseMessage response)
    {
        response.StatusCode.Should().Be(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name, AccountType type)
    {
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new { Name = name, Type = (int)type, Currency = "CAD" });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    /// <summary>Uploads a statement, applies category corrections by description, and commits it.</summary>
    private static async Task CommitStatementAsync(
        HttpClient client, Guid accountId, decimal closingBalance, string asOf,
        IReadOnlyDictionary<string, TransactionCategory> categories, params string[] lines)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("Date,Description,Amount\n" + string.Join('\n', lines) + "\n"));
        file.Headers.ContentType = new("text/csv");
        form.Add(file, "file", "statement.csv");
        form.Add(new StringContent(accountId.ToString()), "accountId");
        var upload = await client.PostAsync("/api/v1/imports", form);
        upload.StatusCode.Should().Be(HttpStatusCode.Created, await upload.Content.ReadAsStringAsync());
        var batch = await upload.Content.ReadFromJsonAsync<JsonElement>();
        var id = batch.GetProperty("id").GetGuid();

        foreach (var row in batch.GetProperty("rows").EnumerateArray())
        {
            if (!categories.TryGetValue(row.GetProperty("description").GetString()!, out var category))
                continue;
            var patch = await client.PatchAsJsonAsync(
                $"/api/v1/imports/{id}/rows/{row.GetProperty("id").GetGuid()}", new { category = (int)category });
            patch.StatusCode.Should().Be(HttpStatusCode.OK);
        }

        var commit = await client.PostAsJsonAsync($"/api/v1/imports/{id}/commit", new
        {
            balances = new[] { new { accountId, closingBalance, asOf } }
        });
        commit.StatusCode.Should().Be(HttpStatusCode.OK, await commit.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UploadReviewCommit_ThenDashboardAndReportTotals_AreCorrect()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var rbc = await CreateAccountAsync(client, "RBC Business Chequing", AccountType.Checking);
        (await client.PutAsJsonAsync($"/api/v1/accounts/{rbc}", new
        {
            Name = "RBC Business Chequing",
            Type = (int)AccountType.Checking,
            Institution = (int)Institution.Rbc,
            Owner = (int)AccountOwner.Corporation,
            ExternalAccountRef = (string?)null
        })).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var savings = await CreateAccountAsync(client, "CIBC Savings", AccountType.Savings);
        var visa = await CreateAccountAsync(client, "CIBC Visa", AccountType.CreditCard);

        (await client.PostAsJsonAsync("/api/v1/budgets", new
        {
            Category = (int)TransactionCategory.HousingAndRent,
            LimitAmount = 2000m, Currency = "CAD", Month = 1, Year = 2026
        })).EnsureSuccessStatusCode();

        // Corporation: a client deposit, pay to the owner, a business expense.
        await CommitStatementAsync(client, rbc, 1800m, "2026-01-31",
            new Dictionary<string, TransactionCategory>
            {
                ["CLIENT INVOICE 1042"] = TransactionCategory.FreelanceIncome,
                ["SOFTWARE SUBSCRIPTION"] = TransactionCategory.EducationAndWork,
            },
            "2026-01-03,CLIENT INVOICE 1042,5000.00",
            "2026-01-15,TRANSFER TO OWNER,-3000.00",
            "2026-01-20,SOFTWARE SUBSCRIPTION,-200.00");

        // Personal savings: the owner pay arrives, rent goes out, a landlord refund, a card payment.
        await CommitStatementAsync(client, savings, 1000m, "2026-01-31",
            new Dictionary<string, TransactionCategory>
            {
                ["RENT JANUARY"] = TransactionCategory.HousingAndRent,
                ["LANDLORD REFUND"] = TransactionCategory.HousingAndRent,
            },
            "2026-01-02,RENT JANUARY,-1500.00",
            "2026-01-16,TRANSFER FROM RBC,3000.00",
            "2026-01-18,PAYMENT TO VISA,-500.00",
            "2026-01-25,LANDLORD REFUND,100.00");

        // Personal card: groceries, a grocery refund, and the payment from savings.
        await CommitStatementAsync(client, visa, 400m, "2026-01-31",
            new Dictionary<string, TransactionCategory>
            {
                ["GROCERY STORE"] = TransactionCategory.GroceriesAndFood,
                ["GROCERY STORE RETURN"] = TransactionCategory.GroceriesAndFood,
            },
            "2026-01-10,GROCERY STORE,-120.00",
            "2026-01-12,GROCERY STORE RETURN,20.00",
            "2026-01-19,PAYMENT THANK YOU,500.00");

        // Every transfer leg found its partner: nothing is left waiting.
        (await JsonAsync(await client.GetAsync("/api/v1/transactions/needs-attention")))
            .GetArrayLength().Should().Be(0);

        // Personal spending by category: corporate rows, owner pay and the card payment are not spending;
        // refunds reduce their category.
        var insights = await JsonAsync(await client.GetAsync("/api/v1/insights/spending/2026/1"));
        insights.GetProperty("totalSpent").GetDecimal().Should().Be(1500m);
        insights.GetProperty("totalIncome").GetDecimal().Should().Be(3000m);
        insights.GetProperty("categoryBreakdown").EnumerateArray()
            .Select(c => (c.GetProperty("category").GetString(), c.GetProperty("amount").GetDecimal()))
            .Should().BeEquivalentTo(new[] { ("HousingAndRent", 1400m), ("GroceriesAndFood", 100m) });

        var budget = (await JsonAsync(await client.GetAsync("/api/v1/budgets/2026/1"))).EnumerateArray().Single();
        budget.GetProperty("category").GetString().Should().Be("HousingAndRent");
        budget.GetProperty("spentAmount").GetDecimal().Should().Be(1400m);

        var income = await JsonAsync(await client.GetAsync("/api/v1/reports/personal-income?year=2026"));
        income.GetProperty("payFromCorporation").GetDecimal().Should().Be(3000m);
        income.GetProperty("otherTaxableIncome").GetDecimal().Should().Be(0m);

        var corp = await JsonAsync(await client.GetAsync("/api/v1/reports/corporation?fiscalYear=2026"));
        corp.GetProperty("revenue").GetDecimal().Should().Be(5000m);
        corp.GetProperty("paidToOwner").GetDecimal().Should().Be(3000m);
        corp.GetProperty("expenses").GetDecimal().Should().Be(200m);
        corp.GetProperty("closingBalance").GetDecimal().Should().Be(1800m);

        // Net worth: savings 1000 − card owed 400 = 600 personal; + corporate 1800.
        var netWorth = await JsonAsync(await client.GetAsync("/api/v1/reports/net-worth?months=12"));
        netWorth.GetProperty("personal").GetDecimal().Should().Be(600m);
        netWorth.GetProperty("includingCorporation").GetDecimal().Should().Be(2400m);
    }
}
