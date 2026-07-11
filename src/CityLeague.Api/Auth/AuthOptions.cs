namespace CityLeague.Api.Auth;

public class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>"Dev" trusts the exchange payload (local only). "B2C" validates a real id_token.</summary>
    public string Mode { get; set; } = "Dev";

    public B2COptions B2C { get; set; } = new();
}

public class B2COptions
{
    /// <summary>Metadata authority, e.g. https://{tenant}.b2clogin.com/{tenant}.onmicrosoft.com/{policy}/v2.0 </summary>
    public string? Authority { get; set; }

    /// <summary>Application (client) id, validated as the token audience.</summary>
    public string? ClientId { get; set; }

    /// <summary>Optional explicit issuer override.</summary>
    public string? Issuer { get; set; }
}
