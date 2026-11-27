using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FinSight.Api.IntegrationTests.Helpers;
using FinSight.Domain.Entities;
using FinSight.Domain.Enums;
using FinSight.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FinSight.Api.IntegrationTests.Features.Imports;

/// <summary>Uploads made-up statements through the real parser, including the real CIBC PDF reader.</summary>
[Collection("Integration")]
public sealed class PdfImportTests(FinSightWebApplicationFactory factory)
    : IClassFixture<FinSightWebApplicationFactory>
{
    private async Task<(HttpClient Client, Guid AccountId)> NewUserWithCibcCardAsync()
    {
        var email = $"pdf-{Guid.NewGuid():N}@test.dev";
        var client = await factory.CreateAuthenticatedClientAsync(email);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        var account = Account.Create(userId, "CIBC Card", AccountType.CreditCard, "CAD", null,
            Institution.Cibc, AccountOwner.Personal, "1234");
        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return (client, account.Id);
    }

    private static async Task<HttpResponseMessage> UploadPdfAsync(HttpClient client)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(CibcStatementPdf.Build());
        file.Headers.ContentType = new("application/pdf");
        form.Add(file, "file", "statement.pdf");
        return await client.PostAsync("/api/v1/imports", form);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    [Fact]
    public async Task UploadPdf_CibcStatement_CreatesBatchWithClosingBalance()
    {
        var (client, accountId) = await NewUserWithCibcCardAsync();

        var response = await UploadPdfAsync(client);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await ReadJsonAsync(response);
        body.GetProperty("institution").GetString().Should().Be("Cibc");
        body.GetProperty("rows").GetArrayLength().Should().Be(CibcStatementPdf.RowCount);
        body.GetProperty("statementClosingBalance").GetDecimal().Should().Be(CibcStatementPdf.ClosingBalance);
        DateOnly.Parse(body.GetProperty("periodEnd").GetString()!).Should().Be(CibcStatementPdf.PeriodEnd);
        body.GetProperty("rows").EnumerateArray()
            .Should().OnlyContain(r => r.GetProperty("accountId").GetGuid() == accountId);
    }

    [Fact]
    public async Task UploadPdf_Twice_SecondBatchAllDuplicates()
    {
        var (client, accountId) = await NewUserWithCibcCardAsync();
        var first = await ReadJsonAsync(await UploadPdfAsync(client));

        var commit = await client.PostAsJsonAsync($"/api/v1/imports/{first.GetProperty("id").GetGuid()}/commit", new
        {
            Balances = new[] { new { AccountId = accountId, ClosingBalance = CibcStatementPdf.ClosingBalance, AsOf = "2026-10-29" } }
        });
        commit.StatusCode.Should().Be(HttpStatusCode.OK, await commit.Content.ReadAsStringAsync());

        var second = await ReadJsonAsync(await UploadPdfAsync(client));

        var rows = second.GetProperty("rows").EnumerateArray().ToList();
        rows.Should().HaveCount(CibcStatementPdf.RowCount);
        rows.Should().OnlyContain(r => r.GetProperty("isDuplicate").GetBoolean() && r.GetProperty("isExcluded").GetBoolean());
    }

    [Fact]
    public async Task UploadCsv_StatementClosingBalanceNull()
    {
        var (client, _) = await NewUserWithCibcCardAsync();
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes("Date,Description,Amount\n2026-01-05,Coffee Shop,-4.50\n"));
        file.Headers.ContentType = new("text/csv");
        form.Add(file, "file", "stmt.csv");

        var response = await client.PostAsync("/api/v1/imports", form);

        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        var body = await ReadJsonAsync(response);
        body.GetProperty("statementClosingBalance").ValueKind.Should().Be(JsonValueKind.Null);
    }
}
