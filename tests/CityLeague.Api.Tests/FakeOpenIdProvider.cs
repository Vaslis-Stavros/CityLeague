using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CityLeague.Api.Auth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CityLeague.Api.Tests;

/// <summary>
/// Stands in for Google/Microsoft/Apple discovery: serves a JWKS the test controls, so id_tokens
/// go through the real validation path with a key we can sign with.
/// </summary>
public sealed class FakeOpenIdProvider : IOpenIdMetadataProvider, IDisposable
{
    public const string TokenEndpoint = "https://provider.test/token";

    private readonly RSA _rsa = RSA.Create(2048);

    public FakeOpenIdProvider()
    {
        SigningKey = new RsaSecurityKey(_rsa) { KeyId = "test-key" };
    }

    public RsaSecurityKey SigningKey { get; }

    /// <summary>Issuer served per authority. Defaults to the authority itself.</summary>
    public Dictionary<string, string> Issuers { get; } = [];

    public Task<OpenIdConnectConfiguration> GetAsync(string authority, CancellationToken ct = default)
    {
        var config = new OpenIdConnectConfiguration
        {
            Issuer = Issuers.TryGetValue(authority, out var issuer) ? issuer : authority,
            AuthorizationEndpoint = $"{authority}/authorize",
            TokenEndpoint = TokenEndpoint,
        };
        config.SigningKeys.Add(SigningKey);
        return Task.FromResult(config);
    }

    public string CreateIdToken(
        string issuer,
        string audience,
        string subject,
        string? email = null,
        bool emailVerified = true,
        string? name = null,
        string? nonce = null,
        IDictionary<string, object>? extraClaims = null,
        DateTime? expires = null)
    {
        var claims = new List<Claim> { new("sub", subject) };
        if (email is not null)
        {
            claims.Add(new Claim("email", email));
            claims.Add(new Claim("email_verified", emailVerified ? "true" : "false", ClaimValueTypes.Boolean));
        }
        if (name is not null)
            claims.Add(new Claim("name", name));
        if (nonce is not null)
            claims.Add(new Claim("nonce", nonce));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expires ?? DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256),
            Claims = extraClaims,
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    public void Dispose() => _rsa.Dispose();
}

/// <summary>Answers the provider's token endpoint with a canned response.</summary>
public sealed class StubTokenEndpointHandler(Func<IDictionary<string, string>, (HttpStatusCode Status, string Body)> respond)
    : HttpMessageHandler
{
    public IDictionary<string, string>? LastRequest { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var body = await request.Content!.ReadAsStringAsync(ct);
        LastRequest = body.Split('&')
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => Uri.UnescapeDataString(pair[0]), pair => Uri.UnescapeDataString(pair[1]));

        var (status, content) = respond(LastRequest);
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8, "application/json"),
        };
    }

    public static string TokenResponse(string idToken) =>
        JsonSerializer.Serialize(new { id_token = idToken, token_type = "Bearer" });
}
