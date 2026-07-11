using System.Security.Cryptography;
using System.Text;
using CityLeague.Core.Dtos;

namespace CityLeague.Api.Auth;

/// <summary>
/// Local-only validator that trusts the exchange payload. Lets the app run end-to-end
/// without a configured B2C tenant. NEVER enable in production.
/// </summary>
public class DevIdentityValidator : IExternalIdentityValidator
{
    public Task<ExternalIdentity?> ValidateAsync(AuthExchangeRequest request, CancellationToken ct = default)
    {
        var provider = string.IsNullOrWhiteSpace(request.Provider) ? "dev" : request.Provider!.Trim().ToLowerInvariant();

        // Prefer an explicit provider user id; otherwise derive a stable id from the email or token.
        var providerUserId = request.ProviderUserId;
        if (string.IsNullOrWhiteSpace(providerUserId))
        {
            if (!string.IsNullOrWhiteSpace(request.Email))
                providerUserId = StableHash(request.Email!);
            else if (!string.IsNullOrWhiteSpace(request.IdToken))
                providerUserId = StableHash(request.IdToken!);
            else
                providerUserId = StableHash($"dev:{provider}");
        }

        if (string.IsNullOrWhiteSpace(providerUserId))
            return Task.FromResult<ExternalIdentity?>(null);

        var subject = $"{provider}:{providerUserId}";
        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? (request.Email?.Split('@').FirstOrDefault()
                ?? $"{char.ToUpperInvariant(provider[0])}{provider[1..]} player")
            : request.DisplayName!.Trim();

        return Task.FromResult<ExternalIdentity?>(new ExternalIdentity(subject, request.Email, displayName, provider));
    }

    private static string StableHash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
    }
}
