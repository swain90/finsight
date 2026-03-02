using FinSight.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Xunit;

namespace FinSight.Api.IntegrationTests.Infrastructure;

public sealed class FinSightWebAppFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("finsight_test")
        .WithUsername("postgres")
        .WithPassword("postgres_test")
        .WithCleanUp(true)
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await context.Database.MigrateAsync();
    }

    public new async Task DisposeAsync() => await _postgres.StopAsync();

    public string GetConnectionString() => _postgres.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null) services.Remove(descriptor);

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_postgres.GetConnectionString()));
        });

        builder.UseEnvironment("Testing");
    }
}
