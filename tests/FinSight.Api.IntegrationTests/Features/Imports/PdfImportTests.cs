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

    private static async Task<HttpResponseMessage> UploadPdfAsync(HttpClient client, byte[]? pdf = null)
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(pdf ?? CibcStatementPdf.Build());
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
            Balances = new[] { new { AccountId = accountId, ClosingBalance = CibcStatementPdf.ClosingBalance, AsOf = CibcStatementPdf.PeriodEnd } }
        });
        commit.StatusCode.Should().Be(HttpStatusCode.OK, await commit.Content.ReadAsStringAsync());

        var second = await ReadJsonAsync(await UploadPdfAsync(client));

        var rows = second.GetProperty("rows").EnumerateArray().ToList();
        rows.Should().HaveCount(CibcStatementPdf.RowCount);
        rows.Should().OnlyContain(r => r.GetProperty("isDuplicate").GetBoolean() && r.GetProperty("isExcluded").GetBoolean());
    }

    [Fact]
    public async Task UploadPdf_ConsecutiveStatements_NoReconciliationWarning()
    {
        var (client, accountId) = await NewUserWithCibcCardAsync();

        // Statement 1: commit with the review screen's pre-fill (the statement's closing balance and period end).
        var first = await ReadJsonAsync(await UploadPdfAsync(client));
        var firstCommit = await client.PostAsJsonAsync($"/api/v1/imports/{first.GetProperty("id").GetGuid()}/commit", new
        {
            Balances = new[]
            {
                new
                {
                    AccountId = accountId,
                    ClosingBalance = first.GetProperty("statementClosingBalance").GetDecimal(),
                    AsOf = DateOnly.Parse(first.GetProperty("periodEnd").GetString()!),
                },
            },
        });
        firstCommit.StatusCode.Should().Be(HttpStatusCode.OK, await firstCommit.Content.ReadAsStringAsync());

        // Statement 2 opens at statement 1's closing balance and carries a charge dated before its period start.
        var secondResponse = await UploadPdfAsync(client, CibcStatementPdf.BuildNext());
        secondResponse.StatusCode.Should().Be(HttpStatusCode.Created, await secondResponse.Content.ReadAsStringAsync());
        var second = await ReadJsonAsync(secondResponse);
        var secondId = second.GetProperty("id").GetGuid();
        second.GetProperty("rows").GetArrayLength().Should().Be(CibcStatementPdf.NextRowCount);
        second.GetProperty("rows").EnumerateArray()
            .Should().Contain(r => DateOnly.Parse(r.GetProperty("occurredOn").GetString()!) < CibcStatementPdf.PeriodEnd);
        var closing = second.GetProperty("statementClosingBalance").GetDecimal();
        var asOf = DateOnly.Parse(second.GetProperty("periodEnd").GetString()!);
        closing.Should().Be(CibcStatementPdf.NextClosingBalance);
        asOf.Should().Be(CibcStatementPdf.NextPeriodEnd);

        var check = await client.GetFromJsonAsync<JsonElement>(
            $"/api/v1/imports/{secondId}/reconciliation?accountId={accountId}" +
            $"&closingBalance={closing.ToString(System.Globalization.CultureInfo.InvariantCulture)}&asOf={asOf:yyyy-MM-dd}");
        check.GetProperty("skipped").GetBoolean().Should().BeFalse();
        check.GetProperty("isBalanced").GetBoolean().Should().BeTrue(check.ToString());
        check.GetProperty("warning").ValueKind.Should().Be(JsonValueKind.Null);

        var secondCommit = await client.PostAsJsonAsync($"/api/v1/imports/{secondId}/commit", new
        {
            Balances = new[] { new { AccountId = accountId, ClosingBalance = closing, AsOf = asOf } },
        });
        secondCommit.StatusCode.Should().Be(HttpStatusCode.OK, await secondCommit.Content.ReadAsStringAsync());
        (await ReadJsonAsync(secondCommit)).GetProperty("reconciliationWarning").ValueKind
            .Should().Be(JsonValueKind.Null);
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
