namespace FinSight.Application.Common.Models;

public sealed record AuthResult(
    Guid UserId,
    string Email,
    string FullName,
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt
);
