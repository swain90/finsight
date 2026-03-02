using System.Security.Claims;
using FinSight.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace FinSight.Infrastructure.Services.Auth;

public sealed class CurrentUserService(IHttpContextAccessor httpContextAccessor) : ICurrentUserService
{
    private ClaimsPrincipal? User => httpContextAccessor.HttpContext?.User;

    public Guid? UserId
    {
        get
        {
            var claim = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
            return Guid.TryParse(claim, out var id) ? id : null;
        }
    }

    public string? Email => User?.FindFirstValue(ClaimTypes.Email) ?? User?.FindFirstValue("email");
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;
}
