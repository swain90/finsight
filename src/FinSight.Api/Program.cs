using System.Text;
using FinSight.Api.Middleware;
using FinSight.Application.DependencyInjection;
using FinSight.Infrastructure.DependencyInjection;
using FinSight.Infrastructure.Services.Jobs;
using Hangfire;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Threading.RateLimiting;
using Scalar.AspNetCore;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog ───────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .Enrich.WithMachineName()
        .WriteTo.Console()
        .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://localhost:5341"));

    // ─── Application & Infrastructure ──────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    // ─── Controllers ───────────────────────────────────────
    builder.Services.AddControllers();

    // ─── JWT Authentication ────────────────────────────────
    var jwtSettings = builder.Configuration.GetSection("JwtSettings");
    var secretKey = jwtSettings["SecretKey"]
        ?? throw new InvalidOperationException("JWT SecretKey not configured.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwtSettings["Issuer"],
                ValidAudience = jwtSettings["Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
                ClockSkew = TimeSpan.Zero  // No drift — critical for short-lived tokens
            };
        });

    builder.Services.AddAuthorization();

    // ─── Rate Limiting ─────────────────────────────────────
    builder.Services.AddRateLimiter(options =>
    {
        options.AddFixedWindowLimiter("auth", o =>
        {
            o.PermitLimit = builder.Configuration.GetValue("RateLimiting:AuthPermitLimit", 5);
            o.Window = TimeSpan.FromMinutes(1);
            o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
            o.QueueLimit = 0;
        });

        options.AddTokenBucketLimiter("api", o =>
        {
            o.TokenLimit = 100;
            o.TokensPerPeriod = 100;
            o.ReplenishmentPeriod = TimeSpan.FromMinutes(1);
        });

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    });

    // ─── CORS ──────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("AllowFrontend", policy =>
            policy.WithOrigins(
                    builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
                    ?? ["http://localhost:3000"])
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials());
    });

    // ─── Health Checks ─────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddNpgSql(builder.Configuration.GetConnectionString("DefaultConnection")!)
        .AddRedis(builder.Configuration.GetConnectionString("Redis")!);

    // ─── Exception Handling ────────────────────────────────
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    // ─── Build Pipeline ────────────────────────────────────
    var app = builder.Build();

    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        // Hangfire dashboard (dev only — add auth middleware in production)
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            IsReadOnlyFunc = _ => false
        });
    }

    app.UseExceptionHandler();
    app.UseHsts();
    if (!app.Environment.IsDevelopment())
    {
        app.UseHttpsRedirection();
    }

    // Security headers
    app.Use(async (context, next) =>
    {
        context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
        context.Response.Headers.Append("X-Frame-Options", "DENY");
        context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
        context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
        await next();
    });

    app.UseCors("AllowFrontend");
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    app.MapHealthChecks("/health/live",
        new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions { Predicate = _ => false });
    app.MapHealthChecks("/health/ready");

    // Register recurring Hangfire jobs
    JobScheduler.RegisterRecurringJobs(
        app.Services.GetRequiredService<IRecurringJobManager>(),
        app.Services.GetRequiredService<ILogger<Program>>());

    Log.Information("FinSight API starting...");
    app.Run();
}
catch (Exception ex)
{
    // dotnet-ef uses design-time host bootstrap and can intentionally abort startup.
    if (ex is not Microsoft.Extensions.Hosting.HostAbortedException)
    {
        Log.Fatal(ex, "Application startup failed.");
    }
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
