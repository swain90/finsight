using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using FinSight.Infrastructure.Persistence;
using Npgsql;
using Respawn;
using Xunit;

namespace FinSight.Api.IntegrationTests.Infrastructure;

public sealed class DatabaseResetFixture(FinSightWebAppFactory factory) : IAsyncLifetime
{
    private Respawner _respawner = default!;
    private string _connectionString = default!;

    public async Task InitializeAsync()
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        _connectionString = context.Database.GetConnectionString()!;

        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        _respawner = await Respawner.CreateAsync(conn, new RespawnerOptions
        {
            DbAdapter = DbAdapter.Postgres,
            TablesToIgnore = [new Respawn.Graph.Table("__EFMigrationsHistory")]
        });
    }

    public async Task DisposeAsync()
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync();
        await _respawner.ResetAsync(conn);
    }
}
