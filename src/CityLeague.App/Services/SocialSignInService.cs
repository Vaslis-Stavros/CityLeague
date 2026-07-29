using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CityLeague.Core.Dtos;
using Microsoft.Maui.Authentication;

namespace CityLeague.App.Services;

/// <summary>A verified credential obtained from an identity provider, ready to exchange for app tokens.</summary>
public record SocialSignInCredential(
    string Provider,
    string? Code = null,
    string? CodeVerifier = null,
    string? RedirectUri = null,
    string? IdToken = null,
    string? Nonce = null,
    string? Email = null,
    string? DisplayName = null);

/// <summary>Raised when the user dismisses the provider's sign-in sheet.</summary>
public class SocialSignInCanceledException() : OperationCanceledException("Sign-in was canceled.");

public interface ISocialSignInService
{
    /// <summary>Sign-in options the API is configured for.</summary>
    Task<AuthProvidersResponse> GetOptionsAsync(CancellationToken ct = default);

    /// <summary>
    /// Runs the provider's interactive sign-in. Returns null when the API has no configuration
    /// for the provider, so the caller can decide how to degrade.
    /// </summary>
    Task<SocialSignInCredential?> SignInAsync(string provider, CancellationToken ct = default);
}

public class SocialSignInService(IHttpClientFactory httpFactory, ApiSettings settings) : ISocialSignInService
{
    private readonly SemaphoreSlim _optionsLock = new(1, 1);
    private AuthProvidersResponse? _options;

    public async Task<AuthProvidersResponse> GetOptionsAsync(CancellationToken ct = default)
    {
        if (_options is not null)
            return _options;

        await _optionsLock.WaitAsync(ct);
        try
        {
            if (_options is null)
                _options = await FetchOptionsAsync(ct);
        }
        finally
        {
            _optionsLock.Release();
        }

        return _options;
    }

    private async Task<AuthProvidersResponse> FetchOptionsAsync(CancellationToken ct)
    {
        var client = httpFactory.CreateClient(AuthService.AuthClientName);
        client.BaseAddress = new Uri(settings.BaseUrl);

        using var response = await client.GetAsync("/api/auth/providers", ct);

        // Older API builds don't expose this endpoint — treat as "no providers, Dev still ok".
        if (response.StatusCode == HttpStatusCode.NotFound)
            return new AuthProvidersResponse(true, []);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new ApiException((int)response.StatusCode,
                string.IsNullOrWhiteSpace(body)
                    ? "Couldn't load sign-in options from the server."
                    : body);
        }

        return await response.Content.ReadFromJsonAsync<AuthProvidersResponse>(cancellationToken: ct)
            ?? new AuthProvidersResponse(false, []);
    }

    public async Task<SocialSignInCredential?> SignInAsync(string provider, CancellationToken ct = default)
    {
        var options = await GetOptionsAsync(ct);
        var descriptor = options.Providers.FirstOrDefault(
            p => string.Equals(p.Provider, provider, StringComparison.OrdinalIgnoreCase));
        if (descriptor is null)
            return null;

#if IOS
        // Apple requires the native sheet on iOS when other social logins are offered.
        if (descriptor.SupportsNativeIos)
            return await SignInWithAppleAsync(descriptor);
#endif

        return await SignInWithBrowserAsync(descriptor);
    }

    private static async Task<SocialSignInCredential> SignInWithBrowserAsync(AuthProviderDto descriptor)
    {
        var verifier = CreateRandomToken(64);
        var state = CreateRandomToken(32);
        var nonce = CreateRandomToken(32);

        var parameters = new Dictionary<string, string>
        {
            ["client_id"] = descriptor.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = descriptor.RedirectUri,
            ["scope"] = descriptor.Scopes,
            ["state"] = state,
            ["nonce"] = nonce,
        };

        if (!string.Equals(descriptor.ResponseMode, "query", StringComparison.OrdinalIgnoreCase))
            parameters["response_mode"] = descriptor.ResponseMode;

        if (descriptor.UsePkce)
        {
            parameters["code_challenge"] = CreateChallenge(verifier);
            parameters["code_challenge_method"] = "S256";
            parameters["prompt"] = "select_account";
        }

        var authorizeUrl = new Uri(BuildUrl(descriptor.AuthorizeUrl, parameters));
        var callbackUrl = new Uri(descriptor.CallbackUrl);

        WebAuthenticatorResult result;
        try
        {
            result = await WebAuthenticator.Default.AuthenticateAsync(new WebAuthenticatorOptions
            {
                Url = authorizeUrl,
                CallbackUrl = callbackUrl,
                PrefersEphemeralWebBrowserSession = true,
            });
        }
        catch (TaskCanceledException)
        {
            throw new SocialSignInCanceledException();
        }
        catch (Exception ex) when (IsAuthenticatorFailure(ex))
        {
            throw new ApiException(401,
                $"{Display(descriptor.Provider)} sign-in didn’t complete. " +
                "Confirm the API is reachable, the redirect URI is registered with the provider, " +
                "and try again.");
        }

        if (result.Properties.TryGetValue("error", out var error) && !string.IsNullOrWhiteSpace(error))
        {
            if (string.Equals(error, "access_denied", StringComparison.OrdinalIgnoreCase)
                || string.Equals(error, "user_cancelled", StringComparison.OrdinalIgnoreCase))
                throw new SocialSignInCanceledException();

            result.Properties.TryGetValue("error_description", out var description);
            throw new ApiException(400, string.IsNullOrWhiteSpace(description) ? error : description!);
        }

        // Some platforms put state/code on the callback URI instead of Properties.
        var returnedState = GetValue(result, "state");
        if (!string.Equals(returnedState, state, StringComparison.Ordinal))
            throw new ApiException(400, "Sign-in could not be verified. Please try again.");

        var code = GetValue(result, "code");
        if (string.IsNullOrWhiteSpace(code))
            throw new ApiException(400, "The provider did not return an authorization code.");

        var (email, displayName) = ReadAppleUser(result);

        return new SocialSignInCredential(
            Provider: descriptor.Provider,
            Code: code,
            CodeVerifier: descriptor.UsePkce ? verifier : null,
            RedirectUri: descriptor.RedirectUri,
            Nonce: nonce,
            Email: email,
            DisplayName: displayName);
    }

#if IOS
    private static async Task<SocialSignInCredential> SignInWithAppleAsync(AuthProviderDto descriptor)
    {
        WebAuthenticatorResult result;
        try
        {
            result = await AppleSignInAuthenticator.Default.AuthenticateAsync(new AppleSignInAuthenticator.Options
            {
                IncludeEmailScope = true,
                IncludeFullNameScope = true,
            });
        }
        catch (TaskCanceledException)
        {
            throw new SocialSignInCanceledException();
        }
        catch (Exception ex) when (IsAuthenticatorFailure(ex))
        {
            throw new ApiException(401, "Apple sign-in didn’t complete. Check the Sign in with Apple entitlement and try again.");
        }

        var idToken = result.IdToken;
        if (string.IsNullOrWhiteSpace(idToken))
            throw new ApiException(400, "Apple did not return an identity token.");

        result.Properties.TryGetValue("email", out var email);
        result.Properties.TryGetValue("name", out var name);

        return new SocialSignInCredential(
            Provider: descriptor.Provider,
            IdToken: idToken,
            Email: string.IsNullOrWhiteSpace(email) ? null : email,
            DisplayName: string.IsNullOrWhiteSpace(name) ? null : name);
    }
#endif

    private static string? GetValue(WebAuthenticatorResult result, string key)
    {
        if (result.Properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            return value;

        // Fallback: parse the callback URI when the platform didn't flatten query params.
        var query = result.CallbackUri?.Query;
        if (string.IsNullOrEmpty(query))
            return null;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length == 0) continue;
            if (!string.Equals(Uri.UnescapeDataString(pieces[0]), key, StringComparison.OrdinalIgnoreCase))
                continue;
            return pieces.Length > 1 ? Uri.UnescapeDataString(pieces[1]) : string.Empty;
        }

        return null;
    }

    private static bool IsAuthenticatorFailure(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        return message.Contains("authentication failed", StringComparison.OrdinalIgnoreCase)
            || message.Contains("webauthenticator", StringComparison.OrdinalIgnoreCase);
    }

    private static (string? Email, string? DisplayName) ReadAppleUser(WebAuthenticatorResult result)
    {
        if (!result.Properties.TryGetValue("user", out var payload) || string.IsNullOrWhiteSpace(payload))
            return (null, null);

        try
        {
            using var json = JsonDocument.Parse(payload);
            var root = json.RootElement;

            string? email = root.TryGetProperty("email", out var emailValue) ? emailValue.GetString() : null;
            string? displayName = null;
            if (root.TryGetProperty("name", out var nameValue))
            {
                var given = nameValue.TryGetProperty("firstName", out var first) ? first.GetString() : null;
                var family = nameValue.TryGetProperty("lastName", out var last) ? last.GetString() : null;
                displayName = string.Join(' ', new[] { given, family }.Where(p => !string.IsNullOrWhiteSpace(p)));
            }

            return (email, string.IsNullOrWhiteSpace(displayName) ? null : displayName);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static string BuildUrl(string endpoint, IDictionary<string, string> parameters)
    {
        var query = string.Join('&', parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));

        return endpoint.Contains('?') ? $"{endpoint}&{query}" : $"{endpoint}?{query}";
    }

    private static string CreateRandomToken(int bytes) => Base64Url(RandomNumberGenerator.GetBytes(bytes));

    private static string CreateChallenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Display(string provider) => provider.ToLowerInvariant() switch
    {
        "google" => "Google",
        "microsoft" => "Microsoft",
        "apple" => "Apple",
        _ => provider,
    };
}
