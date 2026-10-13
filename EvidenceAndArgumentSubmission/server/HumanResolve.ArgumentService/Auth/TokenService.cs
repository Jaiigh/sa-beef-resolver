using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace HumanResolve.ArgumentService.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Authentication:Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
    public int ExpiryMinutes { get; init; } = 60;
}

public sealed record AuthUser(Guid PersonId, string Email, string DisplayName, string PasswordHash, DateTimeOffset CreatedAt);
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenService { AccessToken Create(AuthUser user); }

public sealed class TokenService(JwtOptions options) : ITokenService
{
    public AccessToken Create(AuthUser user)
    {
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(options.ExpiryMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.PersonId.ToString()),
            new Claim(ClaimTypes.NameIdentifier, user.PersonId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName)
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(options.Issuer, options.Audience, claims, expires: expiresAt.UtcDateTime, signingCredentials: credentials);
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expiresAt);
    }
}
