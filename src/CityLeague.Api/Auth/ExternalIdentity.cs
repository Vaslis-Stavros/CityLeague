using CityLeague.Core.Dtos;

namespace CityLeague.Api.Auth;

/// <summary>A verified identity from an external provider (or the dev shim).</summary>
/// <param name="Subject">Provider-prefixed subject, e.g. "google:1039...".</param>
/// <param name="EmailVerified">
/// True when the provider asserts the email is verified. Only then may it be used to link
/// the identity to an existing account.
/// </param>
public record ExternalIdentity(
    string Subject,
    string? Email,
    string? DisplayName,
    string? Provider,
    bool EmailVerified = false);

/// <summary>Validates the incoming auth-exchange payload and returns a trusted identity.</summary>
public interface IExternalIdentityValidator
{
    Task<ExternalIdentity?> ValidateAsync(AuthExchangeRequest request, CancellationToken ct = default);
}

/// <summary>
/// A sign-in attempt that failed for a reason worth showing the user (provider error,
/// missing server configuration) rather than a plain "invalid token".
/// </summary>
public class SocialSignInException(string message) : Exception(message);
