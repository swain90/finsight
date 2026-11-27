using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using FinSight.Infrastructure.Imports;
using FinSight.Infrastructure.Imports.Readers;
using FinSight.Infrastructure.Persistence;
using FinSight.Infrastructure.Persistence.Repositories;
using FinSight.Infrastructure.Services;
using FinSight.Infrastructure.Persistence.Seeders;
using FinSight.Infrastructure.Services.AI;
using FinSight.Infrastructure.Services.Auth;
using FinSight.Infrastructure.Services.Jobs;
using FinSight.Infrastructure.Services.ML;
using FinSight.Infrastructure.Services.Plaid;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenAI;

namespace FinSight.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // ─── Database ──────────────────────────────────────────────────────
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));

        services.AddScoped<IApplicationDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<DatabaseSeeder>();

        // ─── Repositories ──────────────────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IAccountRepository, AccountRepository>();
        services.AddScoped<ITransactionRepository, TransactionRepository>();
        services.AddScoped<IBudgetRepository, BudgetRepository>();
        services.AddScoped<IImportBatchRepository, ImportBatchRepository>();
        services.AddScoped<IMerchantRuleRepository, MerchantRuleRepository>();
        services.AddScoped<IBalanceSnapshotRepository, BalanceSnapshotRepository>();

        // ─── Auth Services ─────────────────────────────────────────────────
        services.AddHttpContextAccessor();
        services.AddScoped<IJwtService, JwtService>();
        services.AddScoped<IStatementParser, StatementParser>();
        services.AddSingleton<IStatementReader, WealthsimpleStatementReader>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();

        // ─── AI (Microsoft.Extensions.AI — model-agnostic) ─────────────────
        var openAiKey = configuration["OpenAI:ApiKey"];
        if (!string.IsNullOrWhiteSpace(openAiKey))
        {
            services.AddSingleton<IChatClient>(_ =>
                new OpenAIClient(openAiKey).AsChatClient("gpt-4o-mini"));
            services.AddScoped<IAiInsightsService, AiInsightsService>();
        }
        else
        {
            services.AddScoped<IAiInsightsService, NoOpAiInsightsService>();
        }

        // ─── ML.NET Services ───────────────────────────────────────────────
        services.AddScoped<IAnomalyDetectionService, AnomalyDetectionService>();
        services.AddScoped<ITransactionCategorizationService, TransactionCategorizationService>();

        // ─── Plaid ─────────────────────────────────────────────────────────
        var plaidBaseUrl = configuration["Plaid:BaseUrl"] ?? "https://sandbox.plaid.com";
        services.AddHttpClient("Plaid", client =>
        {
            client.BaseAddress = new Uri(plaidBaseUrl);
            client.DefaultRequestHeaders.Add("Plaid-Version", "2020-09-14");
        });
        services.AddScoped<IPlaidService, PlaidService>();

        // ─── Background Jobs (Hangfire) ────────────────────────────────────
        services.AddHangfire(cfg => cfg
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(opts =>
                opts.UseNpgsqlConnection(
                    configuration.GetConnectionString("DefaultConnection"))));

        services.AddHangfireServer(opts =>
        {
            opts.WorkerCount = Environment.ProcessorCount * 2;
            opts.Queues = ["critical", "default", "low"];
        });

        services.AddScoped<PlaidSyncJob>();

        // ─── Caching (Redis) ───────────────────────────────────────────────
        services.AddStackExchangeRedisCache(opts =>
            opts.Configuration = configuration.GetConnectionString("Redis"));

        return services;
    }
}

public sealed class NoOpAiInsightsService : IAiInsightsService
{
    private const string DisabledMessage = "AI features are disabled. Set OpenAI:ApiKey to enable insights.";

    public Task<string> GenerateSpendingInsightAsync(string spendingSummary, CancellationToken cancellationToken = default)
        => Task.FromResult(DisabledMessage);

    public Task<string> GenerateBudgetAdviceAsync(string budgetContext, CancellationToken cancellationToken = default)
        => Task.FromResult(DisabledMessage);
}
