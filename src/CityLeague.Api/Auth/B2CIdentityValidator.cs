using CityLeague.Core.Dtos;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CityLeague.Api.Auth;

/// <summary>Validates a real Azure AD B2C id_token against the tenant's published metadata.</summary>
public class B2CIdentityValidator(IOptions<AuthOptions> options, IOpenIdMetadataProvider metadata)
{
    private readonly B2COptions _options = options.Value.B2C;

    public async Task<ExternalIdentity?> ValidateAsync(AuthExchangeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken) || !_options.Enabled)
            return null;

        var config = await metadata.GetAsync(_options.Authority!, ct);
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer ?? config.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.ClientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };

        var handler = new JsonWebTokenHandler { MapInboundClaims = false };
        var result = await handler.ValidateTokenAsync(request.IdToken, parameters);
        if (!result.IsValid || result.SecurityToken is not JsonWebToken token)
            return null;

        var subject = Read(token, "oid") ?? Read(token, "sub");
        if (string.IsNullOrWhiteSpace(subject))
            return null;

        var email = ReadFirstEmail(token) ?? Read(token, "email");
        var name = Read(token, "name") ?? email?.Split('@').FirstOrDefault();
        var provider = Read(token, "idp") ?? "b2c";

        // B2C only surfaces emails it has already verified through the user flow.
        return new ExternalIdentity(subject, email, name, provider, EmailVerified: email is not null);
    }

    private static string? ReadFirstEmail(JsonWebToken token)
    {
        if (token.TryGetPayloadValue<string[]>("emails", out var emails) && emails.Length > 0)
            return emails[0];
        return token.TryGetPayloadValue<string>("emails", out var single) ? single : null;
    }

    private static string? Read(JsonWebToken token, string claim) =>
        token.TryGetPayloadValue<string>(claim, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;
}
