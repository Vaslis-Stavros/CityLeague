namespace CityLeague.Infrastructure.Auth;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "CityLeague";
    public string Audience { get; set; } = "CityLeague-app";

    /// <summary>Symmetric signing key. MUST be overridden in production via configuration/secret.</summary>
    public string SigningKey { get; set; } = "dev-only-signing-key-change-me-please-32b";

    public int AccessTokenMinutes { get; set; } = 60;
    public int RefreshTokenDays { get; set; } = 30;
}
