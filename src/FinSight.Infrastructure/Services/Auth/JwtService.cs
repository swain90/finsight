using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using FinSight.Application.Common.Interfaces;
using FinSight.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace FinSight.Infrastructure.Services.Auth;

public sealed class JwtService(IConfiguration configuration) : IJwtService
{
    public string GenerateAccessToken(User user)
    {
        var jwtSettings = configuration.GetSection("JwtSettings");
        var secretKey = jwtSettings["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiryMinutes = int.Parse(jwtSettings["ExpiryMinutes"] ?? "5");

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.GivenName, user.FirstName),
            new Claim(JwtRegisteredClaimNames.FamilyName, user.LastName),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"] ?? "finsight-api",
            audience: jwtSettings["Audience"] ?? "finsight-client",
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public RefreshToken GenerateRefreshToken(Guid userId, string ipAddress)
        => RefreshToken.Create(
            userId,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
            DateTimeOffset.UtcNow.AddDays(30),
            ipAddress);
}
