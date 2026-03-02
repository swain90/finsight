using System.Net.Http.Json;
using FinSight.Application.Common.Interfaces;
using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace FinSight.Api.IntegrationTests.Helpers;

/// <summary>
/// Shared fixture: one PostgreSQL container per test collection.
/// Implements IAsyncLifetime so xUnit manages container lifecycle.
/// </summary>
public sealed class FinSightWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("finsight_test")
        .WithUsername("postgres")
        .WithPassword("postgres_test")
        .Build();

    public async Task InitializeAsync() => await _postgres.StartAsync();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            // Swap EF Core to use Testcontainers PostgreSQL
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));

            // Replace AI with deterministic test double
            services.RemoveAll<IAiInsightsService>();
            services.AddScoped<IAiInsightsService, NoOpAiInsightsService>();

            // Create schema from EF model (no migrations needed in tests)
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();
        });
    }

    /// <summary>Returns an HttpClient with a valid Bearer token for a freshly-registered test user.</summary>
    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string email = "",
        string password = "TestPass1!")
    {
        email = string.IsNullOrEmpty(email) ? $"user-{Guid.NewGuid():N}@test.dev" : email;
        var client = CreateClient();

        var resp = await client.PostAsJsonAsync("/api/v1/auth/register", new
        {
            Email = email,
            Password = password,
            FirstName = "Test",
            LastName = "User"
        });

        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        var token = body!["accessToken"].ToString()!;

        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    public new async Task DisposeAsync() => await _postgres.DisposeAsync();
}

// ─── Test doubles ──────────────────────────────────────────────────────────────
public sealed class NoOpAiInsightsService : IAiInsightsService
{
    public Task<string> GenerateSpendingInsightAsync(string summary, CancellationToken ct = default)
        => Task.FromResult("[TEST] Your spending looks healthy this month.");

    public Task<string> GenerateBudgetAdviceAsync(string context, CancellationToken ct = default)
        => Task.FromResult("[TEST] You're on track with your budgets.");
}
