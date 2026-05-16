using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Tracker.Entities;
using Tracker.Options;

namespace Tracker.Services;

public interface ITokenService
{
    (string token, DateTime expiresAtUtc) CreateAccessToken(User user, Tenant tenant, Guid plantId, bool isPlatformAdmin);
    (string token, string hash, DateTime expiresAtUtc) CreateRefreshToken();
    string HashToken(string token);
}

public class TokenService : ITokenService
{
    private readonly JwtOptions _jwt;

    public TokenService(IOptions<JwtOptions> jwt) => _jwt = jwt.Value;

    public (string token, DateTime expiresAtUtc) CreateAccessToken(
        User user, Tenant tenant, Guid plantId, bool isPlatformAdmin)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.Email),
            new(TrackerClaims.TenantId, tenant.Id.ToString()),
            new(TrackerClaims.TenantSlug, tenant.Slug),
            new(TrackerClaims.TenantName, tenant.Name),
            new(TrackerClaims.PlantId, plantId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        if (isPlatformAdmin)
            claims.Add(new Claim(TrackerClaims.PlatformAdmin, "true"));

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    public (string token, string hash, DateTime expiresAtUtc) CreateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        var token = Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");
        return (token, HashToken(token), DateTime.UtcNow.AddDays(_jwt.RefreshTokenDays));
    }

    public string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
