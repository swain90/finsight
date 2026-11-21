using System.Net;
using System.Net.Http.Json;
using FinSight.Api.IntegrationTests.Helpers;
using FluentAssertions;
using Xunit;

namespace FinSight.Api.IntegrationTests.Tests;

[Collection("Integration")]
public sealed class ReportsEndpointTests(FinSightWebApplicationFactory factory)
    : IClassFixture<FinSightWebApplicationFactory>
{
    [Fact]
    public async Task UpdateSettings_Feb29_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var response = await client.PutAsJsonAsync("/api/v1/settings",
            new { corporateFiscalYearEndMonth = 2, corporateFiscalYearEndDay = 29 });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("not a valid fiscal year end");
    }

    [Fact]
    public async Task Settings_UpdateThenGet_RoundTrips()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        var put = await client.PutAsJsonAsync("/api/v1/settings",
            new { corporateFiscalYearEndMonth = 6, corporateFiscalYearEndDay = 30 });
        put.StatusCode.Should().Be(HttpStatusCode.OK);

        var get = await client.GetAsync("/api/v1/settings");
        var body = await get.Content.ReadAsStringAsync();
        body.Should().Contain("\"corporateFiscalYearEndMonth\":6").And.Contain("\"corporateFiscalYearEndDay\":30");
    }

    [Fact]
    public async Task Reports_EmptyUser_ReturnOk()
    {
        var client = await factory.CreateAuthenticatedClientAsync();

        (await client.GetAsync("/api/v1/reports/net-worth?months=6")).StatusCode.Should().Be(HttpStatusCode.OK);
        var corp = await client.GetAsync("/api/v1/reports/corporation");
        corp.StatusCode.Should().Be(HttpStatusCode.OK);
        (await corp.Content.ReadAsStringAsync()).Should().Contain("fiscalYearStart");
        (await client.GetAsync("/api/v1/reports/personal-income?year=2026")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task NetWorth_InvalidMonths_Returns400()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        (await client.GetAsync("/api/v1/reports/net-worth?months=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
