using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Auth.Commands.RefreshToken;

public sealed class RefreshTokenCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IApplicationDbContext applicationDbContext,
    IJwtService jwtService) : IRequestHandler<RefreshTokenCommand, Result<AuthResult>>
{
    private static readonly Error InvalidToken =
        Error.Custom("Auth.InvalidToken", "Refresh token is invalid or expired.");

    public async Task<Result<AuthResult>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByRefreshTokenAsync(request.Token, cancellationToken);
        if (user is null)
            return Result.Failure<AuthResult>(InvalidToken);

        var token = user.RefreshTokens.Single(t => t.Token == request.Token);

        // Reuse detection: if this token was already revoked, revoke the ENTIRE family
        // This means a stolen token that gets used will nuke all sessions for safety
        if (token.IsRevoked)
        {
            RevokeDescendants(user, token, request.IpAddress, "Reuse detected — possible token theft");
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return Result.Failure<AuthResult>(InvalidToken);
        }

        if (!token.IsActive)
            return Result.Failure<AuthResult>(InvalidToken);

        // Rotate: revoke old, issue new
        var newRefreshToken = jwtService.GenerateRefreshToken(user.Id, request.IpAddress);
        token.Revoke(request.IpAddress, newRefreshToken.Token);
        user.RefreshTokens.Add(newRefreshToken);
        applicationDbContext.RefreshTokens.Add(newRefreshToken);

        // Prune expired tokens older than 90 days (keep DB clean)
        var cutoff = DateTimeOffset.UtcNow.AddDays(-90);
        var stale = user.RefreshTokens
            .Where(t => !t.IsActive && t.CreatedAt < cutoff)
            .ToList();
        foreach (var s in stale)
            user.RefreshTokens.Remove(s);

        user.RecordLogin();
        var accessToken = jwtService.GenerateAccessToken(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthResult(
            user.Id, user.Email, user.FullName,
            accessToken, newRefreshToken.Token,
            DateTimeOffset.UtcNow.AddMinutes(5)));
    }

    /// <summary>Recursively revokes all tokens in the family chain.</summary>
    private static void RevokeDescendants(Domain.Entities.User user, Domain.Entities.RefreshToken token, string ip, string reason)
    {
        if (string.IsNullOrEmpty(token.ReplacedByToken)) return;

        var child = user.RefreshTokens.FirstOrDefault(t => t.Token == token.ReplacedByToken);
        if (child is null) return;

        if (child.IsActive)
            child.Revoke(ip, reason: reason);
        else
            RevokeDescendants(user, child, ip, reason);
    }
}
