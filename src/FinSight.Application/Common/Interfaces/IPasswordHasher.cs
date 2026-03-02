namespace FinSight.Application.Common.Interfaces;

/// <summary>
/// Abstracts password hashing so Application layer has zero dependency on BCrypt/ASP.NET Identity.
/// Implemented in Infrastructure. Injected into auth handlers via DI.
/// </summary>
public interface IPasswordHasher
{
    /// <summary>Produces a salted, work-factor hash safe for database storage.</summary>
    string Hash(string password);

    /// <summary>
    /// Verifies a plaintext password against a stored hash.
    /// Returns true on match; constant-time to prevent timing attacks.
    /// </summary>
    bool Verify(string password, string hash);
}
