using FinSight.Application.Common.Models;
using FinSight.Domain.Common;
using MediatR;

namespace FinSight.Application.Features.Auth.Commands.RefreshToken;

/// <summary>
/// Exchanges a valid refresh token for a new access + refresh token pair.
/// Old refresh token is revoked (rotation). If a revoked token is presented,
/// the entire token family is revoked to detect theft.
/// </summary>
public sealed record RefreshTokenCommand(
    string Token,
    string IpAddress
) : IRequest<Result<AuthResult>>;
