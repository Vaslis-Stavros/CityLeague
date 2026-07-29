using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CityLeague.Api.Auth;

/// <summary>
/// Apple has no static client secret: the token endpoint expects a short-lived ES256 JWT
/// signed with the team's Sign in with Apple private key.
/// </summary>
public interface IAppleClientSecretFactory
{
    /// <summary>Returns a cached client secret, or null when Apple key material is not configured.</summary>
    string? Create(string clientId);
}

public sealed class AppleClientSecretFactory(IOptions<AuthOptions> options, TimeProvider time) : IAppleClientSecretFactory
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);
    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(30);

    private readonly AppleProviderOptions _apple = options.Value.Apple;
    private readonly Lock _gate = new();

    private string? _cachedSecret;
    private DateTimeOffset _cachedExpiry;

    public string? Create(string clientId)
    {
        if (string.IsNullOrWhiteSpace(_apple.TeamId) || string.IsNullOrWhiteSpace(_apple.KeyId))
            return null;

        var privateKey = ReadPrivateKey();
        if (privateKey is null)
            return null;

        var now = time.GetUtcNow();
        lock (_gate)
        {
            if (_cachedSecret is not null && _cachedExpiry - RenewBefore > now)
                return _cachedSecret;

            using var ecdsa = ECDsa.Create();
            ecdsa.ImportFromPem(privateKey);

            var signingKey = new ECDsaSecurityKey(ecdsa) { KeyId = _apple.KeyId!.Trim() };
            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _apple.TeamId!.Trim(),
                Audience = "https://appleid.apple.com",
                Subject = new System.Security.Claims.ClaimsIdentity([new System.Security.Claims.Claim("sub", clientId)]),
                IssuedAt = now.UtcDateTime,
                NotBefore = now.UtcDateTime,
                Expires = now.Add(Lifetime).UtcDateTime,
                SigningCredentials = new SigningCredentials(signingKey, SecurityAlgorithms.EcdsaSha256),
            };

            _cachedSecret = new JsonWebTokenHandler().CreateToken(descriptor);
            _cachedExpiry = now.Add(Lifetime);
            return _cachedSecret;
        }
    }

    private string? ReadPrivateKey()
    {
        var key = _apple.PrivateKey;
        if (string.IsNullOrWhiteSpace(key) && !string.IsNullOrWhiteSpace(_apple.PrivateKeyPath) && File.Exists(_apple.PrivateKeyPath))
            key = File.ReadAllText(_apple.PrivateKeyPath);

        if (string.IsNullOrWhiteSpace(key))
            return null;

        key = key.Replace("\\n", "\n").Trim();

        // Accept the raw base64 body as well as a full PEM document.
        return key.Contains("-----BEGIN", StringComparison.Ordinal)
            ? key
            : $"-----BEGIN PRIVATE KEY-----\n{key}\n-----END PRIVATE KEY-----";
    }
}
