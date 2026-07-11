using CityLeague.Core.Dtos;

namespace CityLeague.Api.Auth;

/// <summary>A verified identity from an external provider (or the dev shim).</summary>
public record ExternalIdentity(string Subject, string? Email, string? DisplayName, string? Provider);

/// <summary>Validates the incoming auth-exchange payload and returns a trusted identity.</summary>
public interface IExternalIdentityValidator
{
    Task<ExternalIdentity?> ValidateAsync(AuthExchangeRequest request, CancellationToken ct = default);
}
