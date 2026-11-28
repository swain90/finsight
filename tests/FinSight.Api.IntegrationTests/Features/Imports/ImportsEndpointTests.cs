using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinSight.Api.IntegrationTests.Helpers;
using FinSight.Domain.Enums;
using FluentAssertions;
using Xunit;

namespace FinSight.Api.IntegrationTests.Features.Imports;

[Collection("Integration")]
public sealed class ImportsEndpointTests(FinSightWebApplicationFactory factory)
    : IClassFixture<FinSightWebApplicationFactory>
{
    private const string ValidCsv =
        "Date,Description,Amount\n2026-01-05,Coffee Shop,-4.50\n2026-01-06,Payroll,1000.00\n";

    private static async Task<Guid> CreateAccountAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            Name = "Chequing",
            Type = (int)AccountType.Checking,
            Currency = "CAD"
        });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>();
    }

    private static MultipartFormDataContent Upload(string fileName, string csv, Guid? accountId)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new("text/csv");
        form.Add(file, "file", fileName);
        if (accountId is { } id) form.Add(new StringContent(id.ToString()), "accountId");
        return form;
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task Upload_ValidCsv_Returns201WithRows()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var accountId = await CreateAccountAsync(client);

        var response = await client.PostAsync("/api/v1/imports", Upload("stmt.csv", ValidCsv, accountId));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var body = await ReadJsonAsync(response);
        body.GetProperty("rows").GetArrayLength().Should().Be(2);
        body.GetProperty("status").GetString().Should().Be("PendingReview");
    }

    [Fact]
    public async Task Upload_TxtFile_ReturnsValidationFailure()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var accountId = await CreateAccountAsync(client);

        var response = await client.PostAsync("/api/v1/imports", Upload("stmt.txt", ValidCsv, accountId));

        // ValidationBehaviour yields "Validation.Failed", which ToProblemResult maps to 422.
        response.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await ReadJsonAsync(response)).GetProperty("detail").GetString().Should().Contain(".csv");
    }

    [Fact]
    public async Task Upload_UnknownLayout_Returns400WithMessage()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var accountId = await CreateAccountAsync(client);

        var response = await client.PostAsync("/api/v1/imports",
            Upload("stmt.csv", "Foo,Bar\n1,2\n", accountId));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJsonAsync(response)).GetProperty("detail").GetString().Should().Be(
            "Unrecognized file. Expected a CSV export from CIBC, TD, MBNA, RBC or Wealthsimple, or a CIBC credit card PDF statement.");
    }

    [Fact]
    public async Task GetImport_OtherUsersBatch_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var other = await factory.CreateAuthenticatedClientAsync();
        var accountId = await CreateAccountAsync(owner);
        var created = await ReadJsonAsync(
            await owner.PostAsync("/api/v1/imports", Upload("stmt.csv", ValidCsv, accountId)));
        var id = created.GetProperty("id").GetGuid();

        var response = await other.GetAsync($"/api/v1/imports/{id}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Commit_OtherUsersBatch_Returns404()
    {
        var owner = await factory.CreateAuthenticatedClientAsync();
        var other = await factory.CreateAuthenticatedClientAsync();
        var accountId = await CreateAccountAsync(owner);
        var created = await ReadJsonAsync(
            await owner.PostAsync("/api/v1/imports", Upload("stmt.csv", ValidCsv, accountId)));
        var id = created.GetProperty("id").GetGuid();

        var response = await other.PostAsJsonAsync($"/api/v1/imports/{id}/commit", new { balances = Array.Empty<object>() });

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FullFlow_UploadPatchCommitDelete_Succeeds()
    {
        var client = await factory.CreateAuthenticatedClientAsync();
        var accountId = await CreateAccountAsync(client);

        var createResponse = await client.PostAsync("/api/v1/imports", Upload("stmt.csv", ValidCsv, accountId));
        createResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = await ReadJsonAsync(createResponse);
        var id = created.GetProperty("id").GetGuid();
        var rowId = created.GetProperty("rows")[0].GetProperty("id").GetGuid();

        var pendingList = await client.GetAsync("/api/v1/imports");
        pendingList.StatusCode.Should().Be(HttpStatusCode.OK);
        var pendingSummary = (await ReadJsonAsync(pendingList)).EnumerateArray().Single();
        pendingSummary.GetProperty("id").GetGuid().Should().Be(id);
        pendingSummary.GetProperty("status").GetString().Should().Be("PendingReview");
        pendingSummary.GetProperty("rowCount").GetInt32().Should().Be(2);
        pendingSummary.GetProperty("includedRowCount").GetInt32().Should().Be(2);

        var get = await client.GetAsync($"/api/v1/imports/{id}");
        get.StatusCode.Should().Be(HttpStatusCode.OK);
        var fetched = await ReadJsonAsync(get);
        fetched.GetProperty("id").GetGuid().Should().Be(id);
        fetched.GetProperty("status").GetString().Should().Be("PendingReview");
        fetched.GetProperty("rows").GetArrayLength().Should().Be(2);
        fetched.GetProperty("rows")[0].GetProperty("description").GetString().Should().Be("Coffee Shop");
        fetched.GetProperty("rows")[0].GetProperty("amount").GetDecimal().Should().Be(4.50m);
        fetched.GetProperty("rows")[0].GetProperty("type").GetString().Should().Be("Debit");
        fetched.GetProperty("accounts").EnumerateArray().Single().GetProperty("accountId").GetGuid().Should().Be(accountId);

        var patch = await client.PatchAsJsonAsync($"/api/v1/imports/{id}/rows/{rowId}", new { isExcluded = true });
        patch.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(patch)).GetProperty("isExcluded").GetBoolean().Should().BeTrue();

        // First import for the account: no previous snapshot, nothing to compare against.
        var reconcile = await client.GetAsync(
            $"/api/v1/imports/{id}/reconciliation?accountId={accountId}&closingBalance=1000&asOf=2026-01-06");
        reconcile.StatusCode.Should().Be(HttpStatusCode.OK);
        var reconciliation = await ReadJsonAsync(reconcile);
        reconciliation.GetProperty("skipped").GetBoolean().Should().BeTrue();

        var commit = await client.PostAsJsonAsync($"/api/v1/imports/{id}/commit", new
        {
            balances = new[] { new { accountId, closingBalance = 1000m, asOf = "2026-01-06" } }
        });
        commit.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(commit)).GetProperty("status").GetString().Should().Be("Committed");

        var committedSummary = (await ReadJsonAsync(await client.GetAsync("/api/v1/imports"))).EnumerateArray().Single();
        committedSummary.GetProperty("status").GetString().Should().Be("Committed");
        committedSummary.GetProperty("rowCount").GetInt32().Should().Be(2);
        committedSummary.GetProperty("includedRowCount").GetInt32().Should().Be(1);

        var delete = await client.DeleteAsync($"/api/v1/imports/{id}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await client.GetAsync($"/api/v1/imports/{id}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
