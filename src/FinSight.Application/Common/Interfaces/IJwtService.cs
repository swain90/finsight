using FinSight.Domain.Entities;

namespace FinSight.Application.Common.Interfaces;

public interface IJwtService
{
    string GenerateAccessToken(User user);
    RefreshToken GenerateRefreshToken(Guid userId, string ipAddress);
}
