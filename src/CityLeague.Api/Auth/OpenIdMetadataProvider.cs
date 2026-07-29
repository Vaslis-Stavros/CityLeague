using System.Collections.Concurrent;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace CityLeague.Api.Auth;

/// <summary>Resolves (and caches, with automatic key rollover) OIDC discovery documents.</summary>
public interface IOpenIdMetadataProvider
{
    Task<OpenIdConnectConfiguration> GetAsync(string authority, CancellationToken ct = default);
}

public sealed class OpenIdMetadataProvider : IOpenIdMetadataProvider
{
    private readonly ConcurrentDictionary<string, ConfigurationManager<OpenIdConnectConfiguration>> _managers = new();

    public Task<OpenIdConnectConfiguration> GetAsync(string authority, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(authority))
            throw new InvalidOperationException("No OIDC authority configured.");

        var manager = _managers.GetOrAdd(authority.TrimEnd('/'), key => new ConfigurationManager<OpenIdConnectConfiguration>(
            $"{key}/.well-known/openid-configuration",
            new OpenIdConnectConfigurationRetriever(),
            new HttpDocumentRetriever { RequireHttps = true }));

        return manager.GetConfigurationAsync(ct);
    }
}
