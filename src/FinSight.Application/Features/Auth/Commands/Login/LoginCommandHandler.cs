using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Domain.Common;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Auth.Commands.Login;

public sealed class LoginCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IApplicationDbContext applicationDbContext,
    IJwtService jwtService,
    IPasswordHasher passwordHasher) : IRequestHandler<LoginCommand, Result<AuthResult>>
{
    // Intentionally vague — don't reveal whether email exists or password is wrong
    private static readonly Error InvalidCredentials =
        Error.Custom("Auth.InvalidCredentials", "Invalid email or password.");

    public async Task<Result<AuthResult>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // Constant-time comparison — always call Verify even if user not found,
        // so response time doesn't leak whether the email is registered
        var dummyHash = "$2a$12$dummyhashfortimingprotection00000000000000000000000000000";
        var hashToCheck = user?.PasswordHash ?? dummyHash;
        var passwordValid = passwordHasher.Verify(request.Password, hashToCheck);

        if (user is null || !user.IsActive || !passwordValid)
            return Result.Failure<AuthResult>(InvalidCredentials);

        user.RecordLogin();
        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshToken = jwtService.GenerateRefreshToken(user.Id, request.IpAddress);

        // Keep max 5 active tokens per user — evict oldest on overflow
        var activeTokens = user.RefreshTokens
            .Where(t => t.IsActive)
            .OrderBy(t => t.CreatedAt)
            .ToList();

        if (activeTokens.Count >= 5)
            foreach (var old in activeTokens.Take(activeTokens.Count - 4))
                old.Revoke(request.IpAddress, reason: "Evicted due to refresh token cap.");

        user.RefreshTokens.Add(refreshToken);
        applicationDbContext.RefreshTokens.Add(refreshToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthResult(
            user.Id, user.Email, user.FullName,
            accessToken, refreshToken.Token,
            DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}
