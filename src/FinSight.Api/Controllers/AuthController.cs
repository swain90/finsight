using FinSight.Api.Extensions;
using FinSight.Application.Features.Auth.Commands.Login;
using FinSight.Application.Features.Auth.Commands.RefreshToken;
using FinSight.Application.Features.Auth.Commands.Register;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FinSight.Api.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController(IMediator mediator) : ControllerBase
{
    /// <summary>Register a new user account</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var command = new RegisterCommand(request.Email, request.Password, request.FirstName, request.LastName);
        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
            return this.ToProblemResult(result.Error);

        SetRefreshTokenCookie(result.Value.RefreshToken);
        return CreatedAtAction(nameof(Register), new AuthResponse(
            result.Value.UserId, result.Value.Email, result.Value.FullName,
            result.Value.AccessToken, result.Value.ExpiresAt));
    }

    /// <summary>Login with email and password</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var command = new LoginCommand(request.Email, request.Password, ipAddress);
        var result = await mediator.Send(command, ct);

        if (result.IsFailure)
            return Unauthorized(new ProblemDetails { Title = result.Error.Code, Detail = result.Error.Description });

        SetRefreshTokenCookie(result.Value.RefreshToken);
        return Ok(new AuthResponse(
            result.Value.UserId, result.Value.Email, result.Value.FullName,
            result.Value.AccessToken, result.Value.ExpiresAt));
    }

    /// <summary>
    /// Exchange an HTTP-only refresh token cookie for a new access + refresh token pair.
    /// Old refresh token is automatically rotated and revoked.
    /// If a stolen/reused token is detected, the entire session family is revoked.
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken(CancellationToken ct)
    {
        var token = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new ProblemDetails { Title = "Auth.MissingToken", Detail = "Refresh token cookie not found." });

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var result = await mediator.Send(new RefreshTokenCommand(token, ipAddress), ct);

        if (result.IsFailure)
        {
            // Clear the cookie on failure so the browser doesn't keep sending a dead token
            Response.Cookies.Delete("refreshToken");
            return Unauthorized(new ProblemDetails { Title = result.Error.Code, Detail = result.Error.Description });
        }

        SetRefreshTokenCookie(result.Value.RefreshToken);
        return Ok(new AuthResponse(
            result.Value.UserId, result.Value.Email, result.Value.FullName,
            result.Value.AccessToken, result.Value.ExpiresAt));
    }

    /// <summary>Revoke refresh token and clear cookie (logout)</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("refreshToken");
        return NoContent();
    }

    private void SetRefreshTokenCookie(string refreshToken)
    {
        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        });
    }
}

// API request/response models (separate from Application-layer DTOs)
public sealed record RegisterRequest(string Email, string Password, string FirstName, string LastName);
public sealed record LoginRequest(string Email, string Password);
public sealed record AuthResponse(Guid UserId, string Email, string FullName, string AccessToken, DateTimeOffset ExpiresAt);
