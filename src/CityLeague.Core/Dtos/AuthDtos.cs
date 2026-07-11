namespace CityLeague.Core.Dtos;

/// <summary>
/// Exchanges an identity-provider token for an app JWT. In production <see cref="IdToken"/>
/// is the B2C id_token. In local/dev mode the explicit fields are used to provision a user.
/// </summary>
public record AuthExchangeRequest(
    string? IdToken,
    string? Provider = null,
    string? ProviderUserId = null,
    string? Email = null,
    string? DisplayName = null);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserDto User,
    bool NeedsHandle);

public record RefreshRequest(string RefreshToken);

public record LocalRegisterRequest(string Username, string Password, string Email);

public record LocalLoginRequest(string Username, string Password);
