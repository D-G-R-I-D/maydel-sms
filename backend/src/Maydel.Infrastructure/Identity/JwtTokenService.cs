using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Maydel.Application.Abstractions;
using Maydel.Application.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Maydel.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string Section = "Jwt";
    public string Issuer { get; set; } = "maydel-api";
    public string Audience { get; set; } = "maydel-web";

    /// <summary>HMAC signing key, at least 32 characters. Supply via secret store / env var (<c>Jwt__SigningKey</c>), never commit it.</summary>
    public string SigningKey { get; set; } = "";
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 14;

    public SymmetricSecurityKey Key() => new(Encoding.UTF8.GetBytes(SigningKey));
}

/// <summary>Claim names in our access tokens. The BFF may read them; it must not rely on anything else.</summary>
public static class AppClaims
{
    public const string Subject = JwtRegisteredClaimNames.Sub;
    public const string UserName = JwtRegisteredClaimNames.UniqueName;
    public const string Name = "name";
    public const string Role = "role";
    public const string SecurityStamp = "sstamp";

    /// <summary>Present (value "true") while the user still has a temporary password.</summary>
    public const string PasswordChangeRequired = "pwd_change";
}

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(_options.RefreshTokenDays);

    public AccessToken CreateAccessToken(AppUser user, IEnumerable<string> roles)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(AppClaims.Subject, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(AppClaims.UserName, user.UserName ?? ""),
            new(AppClaims.Name, user.FullName),
            new(AppClaims.SecurityStamp, user.SecurityStamp ?? ""),
        };
        claims.AddRange(roles.Select(r => new Claim(AppClaims.Role, r)));
        if (user.MustChangePassword) claims.Add(new Claim(AppClaims.PasswordChangeRequired, "true"));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(_options.Key(), SecurityAlgorithms.HmacSha256),
        });
        return new AccessToken(token, expires);
    }

    public string CreateRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

    public string HashRefreshToken(string refreshToken) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken)));
}
