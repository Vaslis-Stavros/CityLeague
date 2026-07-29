using CityLeague.Core.Dtos;
using Microsoft.Extensions.Options;

namespace CityLeague.Api.Auth;

/// <summary>
/// Describes the configured sign-in providers for the mobile app, resolving each authorize
/// endpoint from the provider's discovery document so the app hardcodes nothing.
/// </summary>
public sealed class SocialProviderDirectory(
    IOptions<AuthOptions> options,
    SocialProviderCatalog catalog,
    IOpenIdMetadataProvider metadata,
    ILogger<SocialProviderDirectory> logger)
{
    public async Task<AuthProvidersResponse> DescribeAsync(CancellationToken ct = default)
    {
        var providers = new List<AuthProviderDto>();

        foreach (var provider in catalog.Enabled)
        {
            string? authorizeUrl = null;
            try
            {
                var config = await metadata.GetAsync(provider.Authority, ct);
                authorizeUrl = config.AuthorizationEndpoint;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Could not read the {Provider} discovery document.", provider.Name);
            }

            if (string.IsNullOrWhiteSpace(authorizeUrl))
                continue;

            providers.Add(new AuthProviderDto(
                Provider: provider.Name,
                ClientId: provider.ClientId,
                AuthorizeUrl: authorizeUrl,
                RedirectUri: provider.RedirectUri,
                CallbackUrl: provider.CallbackUrl,
                Scopes: provider.Scopes,
                ResponseMode: provider.ResponseMode,
                UsePkce: provider.UsePkce,
                SupportsNativeIos: provider.SupportsNativeIos));
        }

        return new AuthProvidersResponse(options.Value.DevSignInEnabled, providers);
    }
}
