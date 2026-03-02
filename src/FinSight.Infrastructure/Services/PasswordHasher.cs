using FinSight.Application.Common.Interfaces;

namespace FinSight.Infrastructure.Services;

/// <summary>
/// BCrypt implementation with work factor 12.
/// Work factor 12 ~= 250ms on a modern CPU — strong enough against GPU cracking,
/// fast enough that login doesn't feel slow. Increase to 13-14 for higher-security
/// deployments once hardware gets faster.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string hash)
    {
        if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(hash))
            return false;

        try
        {
            // EnhancedVerify uses SHA-384 pre-hash to handle passwords > 72 bytes
            // (BCrypt truncates at 72 — this eliminates that vulnerability)
            return BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
        }
        catch
        {
            // Malformed hash — treat as mismatch, never throw to caller
            return false;
        }
    }
}
