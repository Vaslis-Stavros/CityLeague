using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CityLeague.Core.Dtos;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace CityLeague.Api.Auth;

/// <summary>Validates a real Azure AD B2C id_token against the tenant's published metadata.</summary>
public class B2CIdentityValidator : IExternalIdentityValidator
{
    private readonly B2COptions _options;
    private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

    public B2CIdentityValidator(IOptions<AuthOptions> options)
    {
        _options = options.Value.B2C;
        var metadataAddress = $"{_options.Authority?.TrimEnd('/')}/.well-known/openid-configuration";
        _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
            metadataAddress, new OpenIdConnectConfigurationRetriever(), new HttpDocumentRetriever());
    }

    public async Task<ExternalIdentity?> ValidateAsync(AuthExchangeRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
            return null;

        var config = await _configManager.GetConfigurationAsync(ct);
        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer ?? config.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.ClientId,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidateLifetime = true,
        };

        try
        {
            var handler = new JwtSecurityTokenHandler();
            var principal = handler.ValidateToken(request.IdToken, parameters, out _);

            var subject = principal.FindFirst("oid")?.Value
                ?? principal.FindFirst("sub")?.Value
                ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrWhiteSpace(subject))
                return null;

            var email = principal.FindFirst("emails")?.Value
                ?? principal.FindFirst("email")?.Value
                ?? principal.FindFirst(ClaimTypes.Email)?.Value;

            var name = principal.FindFirst("name")?.Value
                ?? principal.FindFirst(ClaimTypes.Name)?.Value
                ?? email?.Split('@').FirstOrDefault();

            var provider = principal.FindFirst("idp")?.Value ?? "b2c";

            return new ExternalIdentity(subject, email, name, provider);
        }
        catch
        {
            return null;
        }
    }
}
