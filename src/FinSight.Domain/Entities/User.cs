using FinSight.Domain.Common;
using FinSight.Domain.Events;

namespace FinSight.Domain.Entities;

public sealed class User : BaseEntity
{
    private readonly List<Account> _accounts = [];

    public string Email { get; private set; } = string.Empty;
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public string FullName => $"{FirstName} {LastName}";
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset? LastLoginAt { get; private set; }

    public IReadOnlyCollection<Account> Accounts => _accounts.AsReadOnly();
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = new List<RefreshToken>();

    private User() { }  // EF Core

    /// <summary>
    /// Create a new user. Password must already be hashed by the caller (Application layer).
    /// Domain stays free of hashing dependencies.
    /// </summary>
    public static User Create(string email, string passwordHash, string firstName, string lastName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        var user = new User
        {
            Email = email.ToLowerInvariant().Trim(),
            PasswordHash = passwordHash,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim()
        };

        user.AddDomainEvent(new UserCreatedEvent(user.Id, user.Email));
        return user;
    }

    public void UpdateProfile(string firstName, string lastName)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        SetUpdatedAt();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTimeOffset.UtcNow;
        SetUpdatedAt();
    }

    /// <summary>Replaces the password hash (e.g. on password reset).</summary>
    public void SetPasswordHash(string newHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(newHash);
        PasswordHash = newHash;
        SetUpdatedAt();
    }

    public void Deactivate()
    {
        IsActive = false;
        SetUpdatedAt();
    }
}
