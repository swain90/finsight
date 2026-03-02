using FinSight.Application.Common.Interfaces;
using FinSight.Application.Common.Models;
using FinSight.Domain.Common;
using FinSight.Domain.Entities;
using FinSight.Domain.Repositories;
using MediatR;

namespace FinSight.Application.Features.Auth.Commands.Register;

public sealed class RegisterCommandHandler(
    IUserRepository userRepository,
    IUnitOfWork unitOfWork,
    IApplicationDbContext applicationDbContext,
    IJwtService jwtService,
    IPasswordHasher passwordHasher) : IRequestHandler<RegisterCommand, Result<AuthResult>>
{
    public async Task<Result<AuthResult>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            return Result.Failure<AuthResult>(Error.Custom("Auth.EmailInUse", "This email is already registered."));

        var passwordHash = passwordHasher.Hash(request.Password);
        var user = User.Create(request.Email, passwordHash, request.FirstName, request.LastName);

        await userRepository.AddAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var accessToken = jwtService.GenerateAccessToken(user);
        var refreshToken = jwtService.GenerateRefreshToken(user.Id, "0.0.0.0");
        user.RefreshTokens.Add(refreshToken);
        applicationDbContext.RefreshTokens.Add(refreshToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthResult(
            user.Id, user.Email, user.FullName,
            accessToken, refreshToken.Token,
            DateTimeOffset.UtcNow.AddMinutes(5)));
    }
}
