using FinSight.Application.Common.Interfaces;

namespace FinSight.Api.IntegrationTests.Helpers;

/// <summary>
/// Lets tests drive MediatR handlers directly as a chosen user. When no test user is set it
/// defers to the real HTTP-based service, so endpoint tests are unaffected.
/// </summary>
public sealed class TestCurrentUserService(ICurrentUserService inner) : ICurrentUserService
{
    private static Guid? _override;

    /// <summary>
    /// The user id handlers see. A plain static rather than an AsyncLocal: a value set inside an async
    /// helper would not flow back to the test. Safe because tests in the "Integration" collection run serially.
    /// </summary>
    public static Guid? Override
    {
        get => _override;
        set => _override = value;
    }

    public Guid? UserId => Override ?? inner.UserId;
    public string? Email => Override is null ? inner.Email : null;
    public bool IsAuthenticated => Override is not null || inner.IsAuthenticated;
}
