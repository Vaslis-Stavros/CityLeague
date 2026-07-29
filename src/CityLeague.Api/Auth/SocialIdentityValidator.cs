using System.Net.Http.Headers;
using System.Text.Json;
using CityLeague.Core.Dtos;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CityLeague.Api.Auth;

public interface ISocialIdentityValidator
{
    Task<ExternalIdentity?> ValidateAsync(
        SocialProviderDescriptor provider, AuthExchangeRequest request, CancellationToken ct = default);
}

/// <summary>
/// Validates a real Google / Microsoft / Apple sign-in: redeems the authorization code at the
/// provider's token endpoint when needed, then verifies the id_token against the provider's
/// published signing keys before trusting any claim.
/// </summary>
public sealed class SocialIdentityValidator(
    IOpenIdMetadataProvider metadata,
    IHttpClientFactory httpFactory,
    IAppleClientSecretFactory appleSecrets,
    ILogger<SocialIdentityValidator> logger) : ISocialIdentityValidator
{
    public const string HttpClientName = "SocialIdentityProviders";

    /// <summary>Microsoft's multi-tenant discovery document templates the issuer per tenant.</summary>
    private const string TenantIdPlaceholder = "{tenantid}";

    private const string MicrosoftConsumerTenantId = "9188040d-6c67-4c5b-b112-36a304b66dad";

    public async Task<ExternalIdentity?> ValidateAsync(
        SocialProviderDescriptor provider, AuthExchangeRequest request, CancellationToken ct = default)
    {
        var idToken = request.IdToken;
        if (!string.IsNullOrWhiteSpace(request.Code))
            idToken = await RedeemCodeAsync(provider, request, ct);

        if (string.IsNullOrWhiteSpace(idToken))
            return null;

        return await ValidateIdTokenAsync(provider, idToken!, request, ct);
    }

    private async Task<string?> RedeemCodeAsync(
        SocialProviderDescriptor provider, AuthExchangeRequest request, CancellationToken ct)
    {
        var config = await metadata.GetAsync(provider.Authority, ct);
        if (string.IsNullOrWhiteSpace(config.TokenEndpoint))
            throw new SocialSignInException($"{provider.Name} did not publish a token endpoint.");

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = request.Code!,
            ["client_id"] = provider.ClientId,
            ["redirect_uri"] = provider.RedirectUri,
        };

        if (provider.UsePkce && !string.IsNullOrWhiteSpace(request.CodeVerifier))
            form["code_verifier"] = request.CodeVerifier!;

        var secret = provider.Name == SocialProviderCatalog.Apple
            ? appleSecrets.Create(provider.ClientId) ?? provider.ClientSecret
            : provider.ClientSecret;
        if (!string.IsNullOrWhiteSpace(secret))
            form["client_secret"] = secret!;

        using var message = new HttpRequestMessage(HttpMethod.Post, config.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(form),
        };
        message.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var client = httpFactory.CreateClient(HttpClientName);
        using var response = await client.SendAsync(message, ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            var detail = ReadError(body);
            logger.LogWarning("{Provider} rejected the authorization code ({Status}): {Detail}",
                provider.Name, (int)response.StatusCode, detail ?? body);
            throw new SocialSignInException(detail is null
                ? $"{Display(provider.Name)} rejected the sign-in. Please try again."
                : $"{Display(provider.Name)} rejected the sign-in: {detail}");
        }

        using var json = JsonDocument.Parse(body);
        if (!json.RootElement.TryGetProperty("id_token", out var idToken))
        {
            logger.LogWarning("{Provider} token response did not contain an id_token.", provider.Name);
            throw new SocialSignInException($"{Display(provider.Name)} did not return an identity token.");
        }

        return idToken.GetString();
    }

    private async Task<ExternalIdentity?> ValidateIdTokenAsync(
        SocialProviderDescriptor provider, string idToken, AuthExchangeRequest request, CancellationToken ct)
    {
        var config = await metadata.GetAsync(provider.Authority, ct);
        var configuredIssuer = config.Issuer ?? provider.Authority;

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            IssuerValidator = (issuer, token, _) => IsIssuerAllowed(issuer, configuredIssuer, token)
                ? issuer
                : throw new SecurityTokenInvalidIssuerException($"Unexpected issuer '{issuer}'."),
            ValidateAudience = true,
            ValidAudiences = provider.Audiences,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        var handler = new JsonWebTokenHandler { MapInboundClaims = false };
        var result = await handler.ValidateTokenAsync(idToken, parameters);
        if (!result.IsValid || result.SecurityToken is not JsonWebToken token)
        {
            logger.LogWarning(result.Exception, "Rejected a {Provider} id_token.", provider.Name);
            return null;
        }

        // A nonce is only present when the app started the flow; when it is, it must match.
        if (!string.IsNullOrWhiteSpace(request.Nonce)
            && token.TryGetPayloadValue<string>("nonce", out var nonce)
            && !string.Equals(nonce, request.Nonce, StringComparison.Ordinal))
        {
            logger.LogWarning("Rejected a {Provider} id_token because the nonce did not match.", provider.Name);
            return null;
        }

        var subject = ReadSubject(provider.Name, token);
        if (string.IsNullOrWhiteSpace(subject))
            return null;

        var email = Read(token, "email") ?? Read(token, "preferred_username");
        if (email is not null && !email.Contains('@'))
            email = null;

        var displayName = Read(token, "name")
            ?? JoinNames(Read(token, "given_name"), Read(token, "family_name"))
            // Apple never includes the name in the id_token; it is only returned once, to the client.
            ?? Trim(request.DisplayName)
            ?? email?.Split('@').FirstOrDefault();

        return new ExternalIdentity(
            Subject: $"{provider.Name}:{subject}",
            Email: email?.Trim().ToLowerInvariant(),
            DisplayName: displayName,
            Provider: provider.Name,
            EmailVerified: email is not null && IsEmailVerified(provider, token));
    }

    private static string? ReadSubject(string provider, JsonWebToken token)
    {
        // Microsoft's "oid" is stable for the user across the tenant; "sub" is per-application.
        if (provider == SocialProviderCatalog.Microsoft && Read(token, "oid") is { } oid)
            return oid;
        return Read(token, "sub");
    }

    private static bool IsEmailVerified(SocialProviderDescriptor provider, JsonWebToken token)
    {
        if (provider.TrustUnverifiedEmail)
            return true;

        if (provider.Name == SocialProviderCatalog.Microsoft)
        {
            // Entra only guarantees the email when the domain is verified ("xms_edov") or the
            // account is a consumer Microsoft account.
            if (ReadBool(token, "xms_edov") == true)
                return true;
            return Read(token, "tid") == MicrosoftConsumerTenantId;
        }

        return ReadBool(token, "email_verified") == true;
    }

    private static bool IsIssuerAllowed(string issuer, string configuredIssuer, SecurityToken token)
    {
        if (string.Equals(issuer, configuredIssuer, StringComparison.Ordinal))
            return true;

        if (!configuredIssuer.Contains(TenantIdPlaceholder, StringComparison.OrdinalIgnoreCase))
            return false;

        var tenantId = token is JsonWebToken jwt ? Read(jwt, "tid") : null;
        if (string.IsNullOrWhiteSpace(tenantId))
            return false;

        var expected = configuredIssuer.Replace(TenantIdPlaceholder, tenantId, StringComparison.OrdinalIgnoreCase);
        return string.Equals(issuer, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static string? Read(JsonWebToken token, string claim) =>
        token.TryGetPayloadValue<string>(claim, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    /// <summary>Providers are inconsistent about booleans: Apple sends "true", Google sends true.</summary>
    private static bool? ReadBool(JsonWebToken token, string claim)
    {
        if (token.TryGetPayloadValue<bool>(claim, out var flag))
            return flag;
        if (token.TryGetPayloadValue<string>(claim, out var text) && bool.TryParse(text, out var parsed))
            return parsed;
        return null;
    }

    private static string? ReadError(string body)
    {
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("error_description", out var description))
                return description.GetString();
            if (json.RootElement.TryGetProperty("error", out var error))
                return error.GetString();
        }
        catch (JsonException)
        {
            // Non-JSON error body; the caller logs the raw text.
        }

        return null;
    }

    private static string? JoinNames(string? given, string? family)
    {
        var joined = string.Join(' ', new[] { given, family }.Where(n => !string.IsNullOrWhiteSpace(n)));
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string Display(string provider) => provider switch
    {
        SocialProviderCatalog.Google => "Google",
        SocialProviderCatalog.Microsoft => "Microsoft",
        SocialProviderCatalog.Apple => "Apple",
        _ => provider,
    };
}
