namespace CityLeague.Api.Auth;

public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// "Dev" additionally trusts the exchange payload without a token (local only).
    /// Any other value ("Production", "B2C") disables that shim. Social providers and B2C
    /// work in every mode as soon as they are configured.
    /// </summary>
    public string Mode { get; set; } = "Dev";

    /// <summary>Custom scheme the mobile app registers for OAuth callbacks.</summary>
    public string MobileRedirectUri { get; set; } = "cityleague://auth/callback";

    /// <summary>
    /// Public https base url of this API (e.g. https://api.cityleague.com). Required for
    /// Apple, which only accepts https redirect URIs.
    /// </summary>
    public string? PublicBaseUrl { get; set; }

    public B2COptions B2C { get; set; } = new();

    public SocialProviderOptions Google { get; set; } = new();

    public SocialProviderOptions Microsoft { get; set; } = new();

    public AppleProviderOptions Apple { get; set; } = new();

    /// <summary>True when the password-less dev shim is allowed.</summary>
    public bool DevSignInEnabled => string.Equals(Mode, "Dev", StringComparison.OrdinalIgnoreCase);
}

public class B2COptions
{
    /// <summary>Metadata authority, e.g. https://{tenant}.b2clogin.com/{tenant}.onmicrosoft.com/{policy}/v2.0 </summary>
    public string? Authority { get; set; }

    /// <summary>Application (client) id, validated as the token audience.</summary>
    public string? ClientId { get; set; }

    /// <summary>Optional explicit issuer override.</summary>
    public string? Issuer { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(Authority) && !string.IsNullOrWhiteSpace(ClientId);
}

/// <summary>Configuration for one OpenID Connect sign-in provider (Google / Microsoft / Apple).</summary>
public class SocialProviderOptions
{
    /// <summary>OIDC authority used for discovery. Defaults to the provider's well-known authority.</summary>
    public string? Authority { get; set; }

    /// <summary>Client (application) id. Leaving this empty disables the provider.</summary>
    public string? ClientId { get; set; }

    /// <summary>Only needed for confidential clients (e.g. a Google "Web application" client).</summary>
    public string? ClientSecret { get; set; }

    /// <summary>Space-delimited scopes. Defaults to the provider's minimum profile scopes.</summary>
    public string? Scopes { get; set; }

    /// <summary>Redirect uri registered with the provider. Defaults to the app's custom scheme.</summary>
    public string? RedirectUri { get; set; }

    /// <summary>
    /// Extra accepted audiences, for platform-specific client ids that issue tokens for the
    /// same user pool (e.g. separate Android/iOS Google clients).
    /// </summary>
    public IList<string> AdditionalAudiences { get; set; } = new List<string>();

    /// <summary>
    /// Accept the provider's email claim for account linking even when the token carries no
    /// verification claim. Only enable for providers whose emails you trust.
    /// </summary>
    public bool TrustUnverifiedEmail { get; set; }

    public bool Enabled => !string.IsNullOrWhiteSpace(ClientId);
}

public class AppleProviderOptions : SocialProviderOptions
{
    /// <summary>Apple developer team id (the client secret's "iss").</summary>
    public string? TeamId { get; set; }

    /// <summary>Id of the Sign in with Apple private key (the "kid" header).</summary>
    public string? KeyId { get; set; }

    /// <summary>Contents of the .p8 private key. Takes precedence over <see cref="PrivateKeyPath"/>.</summary>
    public string? PrivateKey { get; set; }

    /// <summary>Path to the .p8 private key file.</summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    /// iOS bundle id. Native Sign in with Apple issues tokens whose audience is the bundle id
    /// rather than the services id, so it is accepted as an additional audience.
    /// </summary>
    public string? BundleId { get; set; }
}
