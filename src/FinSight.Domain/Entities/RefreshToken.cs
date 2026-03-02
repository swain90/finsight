namespace FinSight.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public string Token { get; init; } = string.Empty;
    public DateTimeOffset ExpiryDate { get; init; }
    public bool IsRevoked { get; private set; }
    public string? ReplacedByToken { get; private set; }
    public string? CreatedByIp { get; init; }
    public string? RevokedByIp { get; private set; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? RevokedAt { get; private set; }

    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiryDate;
    public bool IsActive => !IsRevoked && !IsExpired;

    public User User { get; init; } = null!;

    private RefreshToken() { }  // EF Core

    public static RefreshToken Create(Guid userId, string token, DateTimeOffset expiry, string? ip = null)
        => new() { UserId = userId, Token = token, ExpiryDate = expiry, CreatedByIp = ip };

    /// <summary>
    /// Revokes this token. Optionally records what token replaced it (for family tracking).
    /// </summary>
    public void Revoke(string? ip = null, string? replacedByToken = null, string? reason = null)
    {
        IsRevoked = true;
        RevokedAt = DateTimeOffset.UtcNow;
        RevokedByIp = ip;
        ReplacedByToken = replacedByToken ?? reason; // overloaded field — replacement token OR free-text reason
    }
}
