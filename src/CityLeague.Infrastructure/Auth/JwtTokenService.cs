using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CityLeague.Core.Abstractions;
using CityLeague.Core.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CityLeague.Infrastructure.Auth;

/// <summary>Claim types used across the app's JWTs.</summary>
public static class AppClaims
{
    public const string UserId = "uid";
    public const string Handle = "handle";
    public const string TokenType = "typ";
    public const string Access = "access";
    public const string Refresh = "refresh";
}

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;
    private readonly SymmetricSecurityKey _key;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
    }

    public AuthTokens Issue(User user)
    {
        var now = DateTimeOffset.UtcNow;
        var accessExpires = now.AddMinutes(_options.AccessTokenMinutes);
        var refreshExpires = now.AddDays(_options.RefreshTokenDays);

        var access = BuildToken(user, AppClaims.Access, accessExpires, includeProfile: true);
        var refresh = BuildToken(user, AppClaims.Refresh, refreshExpires, includeProfile: false);

        return new AuthTokens(access, refresh, accessExpires);
    }

    public ClaimsPrincipal? ValidateRefreshToken(string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler();
        try
        {
            var principal = handler.ValidateToken(refreshToken, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _options.Issuer,
                ValidateAudience = true,
                ValidAudience = _options.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = _key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
            }, out _);

            var type = principal.FindFirst(AppClaims.TokenType)?.Value;
            return type == AppClaims.Refresh ? principal : null;
        }
        catch
        {
            return null;
        }
    }

    private string BuildToken(User user, string tokenType, DateTimeOffset expires, bool includeProfile)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(AppClaims.UserId, user.Id.ToString()),
            new(AppClaims.TokenType, tokenType),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (includeProfile)
        {
            claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            if (!string.IsNullOrEmpty(user.UniqueHandle))
                claims.Add(new Claim(AppClaims.Handle, user.UniqueHandle));
            if (!string.IsNullOrEmpty(user.DisplayName))
                claims.Add(new Claim(ClaimTypes.Name, user.DisplayName));
            if (!string.IsNullOrEmpty(user.Email))
                claims.Add(new Claim(JwtRegisteredClaimNames.Email, user.Email));
        }

        var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires.UtcDateTime,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
