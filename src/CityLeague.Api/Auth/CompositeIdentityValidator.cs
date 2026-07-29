using CityLeague.Core.Dtos;
using Microsoft.Extensions.Options;

namespace CityLeague.Api.Auth;

/// <summary>
/// Routes an exchange request to the right validator: a configured social provider first,
/// then Azure AD B2C, and finally the dev shim when it is enabled.
/// </summary>
public sealed class CompositeIdentityValidator(
    IOptions<AuthOptions> options,
    SocialProviderCatalog catalog,
    ISocialIdentityValidator social,
    B2CIdentityValidator b2c,
    DevIdentityValidator dev) : IExternalIdentityValidator
{
    private readonly AuthOptions _options = options.Value;

    public async Task<ExternalIdentity?> ValidateAsync(AuthExchangeRequest request, CancellationToken ct = default)
    {
        var hasCredential = !string.IsNullOrWhiteSpace(request.IdToken) || !string.IsNullOrWhiteSpace(request.Code);

        if (hasCredential && catalog.TryGet(request.Provider, out var provider))
        {
            var identity = await social.ValidateAsync(provider, request, ct);
            if (identity is not null)
                return identity;

            // A real credential that fails verification must never fall through to the dev shim.
            return null;
        }

        if (hasCredential && _options.B2C.Enabled && !string.IsNullOrWhiteSpace(request.IdToken))
            return await b2c.ValidateAsync(request, ct);

        if (_options.DevSignInEnabled)
            return await dev.ValidateAsync(request, ct);

        if (hasCredential)
            throw new SocialSignInException($"Sign-in with '{request.Provider}' is not configured on the server.");

        return null;
    }
}
