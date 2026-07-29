namespace CityLeague.Core.Dtos;

/// <summary>
/// Exchanges an identity-provider credential for an app JWT. Either <see cref="IdToken"/>
/// (already-obtained OIDC id_token, e.g. native Sign in with Apple) or <see cref="Code"/>
/// (authorization code the API redeems at the provider's token endpoint) is required.
/// In Dev auth mode the explicit fields alone are enough to provision a user.
/// </summary>
public record AuthExchangeRequest(
    string? IdToken,
    string? Provider = null,
    string? ProviderUserId = null,
    string? Email = null,
    string? DisplayName = null,
    string? Code = null,
    string? CodeVerifier = null,
    string? RedirectUri = null,
    string? Nonce = null);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserDto User,
    bool NeedsHandle);

public record RefreshRequest(string RefreshToken);

public record LocalRegisterRequest(string Username, string Password, string Email);

public record LocalLoginRequest(string Username, string Password);

/// <summary>
/// Sign-in options the API is configured for. The app uses this to build the provider
/// authorization request, so client ids and endpoints live in API configuration only.
/// </summary>
public record AuthProvidersResponse(
    bool DevSignInEnabled,
    IReadOnlyList<AuthProviderDto> Providers);

/// <param name="Provider">Normalized key: "google", "microsoft" or "apple".</param>
/// <param name="RedirectUri">Redirect the provider is registered with; also used when redeeming the code.</param>
/// <param name="CallbackUrl">Url the app itself listens on. Differs from <paramref name="RedirectUri"/> when the API bridges the callback.</param>
/// <param name="ResponseMode">"query" or "form_post".</param>
/// <param name="SupportsNativeIos">True when the platform offers a native flow (Sign in with Apple on iOS).</param>
public record AuthProviderDto(
    string Provider,
    string ClientId,
    string AuthorizeUrl,
    string RedirectUri,
    string CallbackUrl,
    string Scopes,
    string ResponseMode,
    bool UsePkce,
    bool SupportsNativeIos);
