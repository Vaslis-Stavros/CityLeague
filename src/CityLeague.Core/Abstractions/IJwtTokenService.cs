using System.Security.Claims;
using CityLeague.Core.Entities;

namespace CityLeague.Core.Abstractions;

public record AuthTokens(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt);

/// <summary>Issues and validates the application's own JWT access/refresh tokens.</summary>
public interface IJwtTokenService
{
    AuthTokens Issue(User user);

    /// <summary>Validates a refresh token and returns its principal, or null if invalid/expired.</summary>
    ClaimsPrincipal? ValidateRefreshToken(string refreshToken);
}
