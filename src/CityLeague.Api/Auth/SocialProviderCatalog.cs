using Microsoft.Extensions.Options;

namespace CityLeague.Api.Auth;

/// <summary>Everything needed to run one provider's authorization-code flow.</summary>
public sealed record SocialProviderDescriptor(
    string Name,
    string Authority,
    string ClientId,
    string? ClientSecret,
    string Scopes,
    string RedirectUri,
    string CallbackUrl,
    IReadOnlyList<string> Audiences,
    bool UsePkce,
    string ResponseMode,
    bool SupportsNativeIos,
    bool TrustUnverifiedEmail);

/// <summary>
/// Turns <see cref="AuthOptions"/> into per-provider descriptors, filling in each provider's
/// well-known authority, scopes and redirect conventions.
/// </summary>
public sealed class SocialProviderCatalog
{
    public const string Google = "google";
    public const string Microsoft = "microsoft";
    public const string Apple = "apple";

    /// <summary>
    /// Providers that cannot (Apple) or should not (Google web clients) redirect to a custom
    /// scheme land here, and the API forwards the result to the app's scheme.
    /// </summary>
    public const string CallbackPathPrefix = "/api/auth/callback";

    private readonly Dictionary<string, SocialProviderDescriptor> _providers;

    public SocialProviderCatalog(IOptions<AuthOptions> options)
    {
        var auth = options.Value;
        _providers = new Dictionary<string, SocialProviderDescriptor>(StringComparer.OrdinalIgnoreCase);

        // Google "Web application" clients reject custom-scheme redirects, so the API bridges the
        // callback whenever it has a public https url. Platform-specific (Android/iOS) client ids
        // can override Auth:Google:RedirectUri with their reverse-DNS scheme instead.
        Add(Google, auth.Google, auth, "https://accounts.google.com", "openid email profile",
            usePkce: true, responseMode: "query", supportsNativeIos: false, bridgeCallback: true);

        // Registered as a "Mobile and desktop" redirect, so the custom scheme works directly.
        Add(Microsoft, auth.Microsoft, auth, "https://login.microsoftonline.com/common/v2.0", "openid email profile",
            usePkce: true, responseMode: "query", supportsNativeIos: false, bridgeCallback: false);

        // Apple only accepts https redirects and posts the result as a form.
        Add(Apple, auth.Apple, auth, "https://appleid.apple.com", "name email",
            usePkce: false, responseMode: "form_post", supportsNativeIos: true, bridgeCallback: true);
    }

    public IReadOnlyCollection<SocialProviderDescriptor> Enabled => _providers.Values;

    public bool TryGet(string? provider, out SocialProviderDescriptor descriptor)
    {
        descriptor = null!;
        return !string.IsNullOrWhiteSpace(provider) && _providers.TryGetValue(Normalize(provider)!, out descriptor!);
    }

    public static string? Normalize(string? provider) => provider?.Trim().ToLowerInvariant();

    private void Add(
        string name,
        SocialProviderOptions provider,
        AuthOptions auth,
        string defaultAuthority,
        string defaultScopes,
        bool usePkce,
        string responseMode,
        bool supportsNativeIos,
        bool bridgeCallback)
    {
        if (!provider.Enabled)
            return;

        var callbackUrl = string.IsNullOrWhiteSpace(auth.MobileRedirectUri)
            ? "cityleague://auth/callback"
            : auth.MobileRedirectUri.Trim();

        var redirectUri = provider.RedirectUri?.Trim();
        if (string.IsNullOrWhiteSpace(redirectUri))
        {
            redirectUri = bridgeCallback
                ? BuildBridgedRedirectUri(auth, name)
                : callbackUrl;
        }

        // Apple (and a Google web client) need a public https redirect. Without one there is no
        // usable flow, so the provider stays disabled instead of failing at sign-in time.
        if (string.IsNullOrWhiteSpace(redirectUri))
            return;

        var audiences = new List<string> { provider.ClientId!.Trim() };
        audiences.AddRange(provider.AdditionalAudiences
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .Select(a => a.Trim()));
        if (provider is AppleProviderOptions { BundleId: { } bundleId } && !string.IsNullOrWhiteSpace(bundleId))
            audiences.Add(bundleId.Trim());

        _providers[name] = new SocialProviderDescriptor(
            Name: name,
            Authority: (provider.Authority ?? defaultAuthority).TrimEnd('/'),
            ClientId: provider.ClientId!.Trim(),
            ClientSecret: provider.ClientSecret,
            Scopes: string.IsNullOrWhiteSpace(provider.Scopes) ? defaultScopes : provider.Scopes!.Trim(),
            RedirectUri: redirectUri!,
            CallbackUrl: callbackUrl,
            Audiences: audiences.Distinct(StringComparer.Ordinal).ToArray(),
            UsePkce: usePkce,
            ResponseMode: responseMode,
            SupportsNativeIos: supportsNativeIos,
            TrustUnverifiedEmail: provider.TrustUnverifiedEmail);
    }

    private static string? BuildBridgedRedirectUri(AuthOptions auth, string provider) =>
        string.IsNullOrWhiteSpace(auth.PublicBaseUrl)
            ? null
            : $"{auth.PublicBaseUrl.TrimEnd('/')}{CallbackPathPrefix}/{provider}";
}
