// =============================================================================
// ApplicationServiceExtensions.cs — Application Layer DI Registration
// =============================================================================
// All Application-layer services are registered here via an extension method.
// The API project calls builder.Services.AddApplicationServices() in Program.cs.
//
// This follows the "Composition Root" pattern — all DI wiring happens at startup,
// making the dependency graph explicit and testable.
// =============================================================================

using System.Reflection;
using FinSight.Application.Common.Behaviours;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace FinSight.Application.DependencyInjection;

/// <summary>
/// Extension methods to register Application layer services.
/// Call builder.Services.AddApplicationServices() in Program.cs.
/// </summary>
public static class ApplicationServiceExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // =====================================================================
        // MediatR — auto-discovers all IRequestHandler<,> implementations
        // in this assembly. No manual registration needed per handler.
        // =====================================================================
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);

            // Pipeline behaviours run in REGISTRATION ORDER:
            //   1. ValidationBehaviour  — reject invalid requests early
            //   2. LoggingBehaviour     — time every request, log errors
            // If you add TransactionBehaviour, it goes here too.
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehaviour<,>));
        });

        // =====================================================================
        // FluentValidation — auto-discovers all AbstractValidator<T> implementations
        // in this assembly. The ValidationBehaviour injects IEnumerable<IValidator<T>>
        // which DI populates from these registrations.
        // =====================================================================
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
