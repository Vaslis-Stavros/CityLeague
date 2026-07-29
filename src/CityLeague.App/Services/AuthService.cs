using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CityLeague.Core.Dtos;

namespace CityLeague.App.Services;

public interface IAuthService
{
    UserDto? CurrentUser { get; }
    bool NeedsHandle { get; }
    bool IsAuthenticated { get; }

    Task<bool> LoadSessionAsync();
    Task LoginLocalAsync(string username, string password);
    Task RegisterLocalAsync(string username, string password, string email);
    Task LoginSocialAsync(string provider);
    Task<bool> TryRefreshAsync();
    Task LogoutAsync();
    void UpdateCurrentUser(UserDto user);
}

public class AuthService(
    IHttpClientFactory httpFactory,
    ApiSettings settings,
    ITokenStore tokens,
    ISocialSignInService social) : IAuthService
{
    public const string AuthClientName = "CityLeagueAuth";

    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public UserDto? CurrentUser { get; private set; }
    public bool NeedsHandle { get; private set; }
    public bool IsAuthenticated => CurrentUser is not null && tokens.HasTokens;

    private HttpClient CreateClient()
    {
        var client = httpFactory.CreateClient(AuthClientName);
        client.BaseAddress = new Uri(settings.BaseUrl);
        return client;
    }

    public async Task<bool> LoadSessionAsync()
    {
        await tokens.LoadAsync();
        if (!tokens.HasTokens)
            return false;

        var me = await FetchMeAsync(tokens.AccessToken!);
        if (me is null && await TryRefreshAsync())
            me = CurrentUser;

        if (me is null)
        {
            await LogoutAsync();
            return false;
        }

        CurrentUser = me;
        NeedsHandle = string.IsNullOrEmpty(me.Handle);
        return true;
    }

    public Task LoginLocalAsync(string username, string password)
        => ExchangeAsync("/api/auth/login", new LocalLoginRequest(username, password));

    public Task RegisterLocalAsync(string username, string password, string email)
        => ExchangeAsync("/api/auth/register", new LocalRegisterRequest(username, password, email));

    public async Task LoginSocialAsync(string provider)
    {
        var credential = await social.SignInAsync(provider);
        if (credential is null)
        {
            // Nothing configured for this provider: the dev shim is the only way through.
            var options = await social.GetOptionsAsync();
            if (!options.DevSignInEnabled)
                throw new ApiException(501, $"{DisplayName(provider)} sign-in isn't set up on the server yet.");

            await ExchangeAsync("/api/auth/exchange", new AuthExchangeRequest(IdToken: null, Provider: provider));
            return;
        }

        var request = new AuthExchangeRequest(
            IdToken: credential.IdToken,
            Provider: credential.Provider,
            Email: credential.Email,
            DisplayName: credential.DisplayName,
            Code: credential.Code,
            CodeVerifier: credential.CodeVerifier,
            RedirectUri: credential.RedirectUri,
            Nonce: credential.Nonce);

        await ExchangeAsync("/api/auth/exchange", request);
    }

    private static string DisplayName(string provider) => provider.ToLowerInvariant() switch
    {
        "google" => "Google",
        "microsoft" => "Microsoft",
        "apple" => "Apple",
        _ => provider,
    };

    public async Task<bool> TryRefreshAsync()
    {
        if (string.IsNullOrEmpty(tokens.RefreshToken))
            return false;

        await _refreshLock.WaitAsync();
        try
        {
            var client = CreateClient();
            var response = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken!));
            if (!response.IsSuccessStatusCode)
                return false;

            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (auth is null)
                return false;

            await tokens.SaveAsync(auth.AccessToken, auth.RefreshToken);
            CurrentUser = auth.User;
            NeedsHandle = auth.NeedsHandle;
            return true;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task LogoutAsync()
    {
        await tokens.ClearAsync();
        CurrentUser = null;
        NeedsHandle = false;
    }

    public void UpdateCurrentUser(UserDto user)
    {
        CurrentUser = user;
        NeedsHandle = string.IsNullOrEmpty(user.Handle);
    }

    private async Task ExchangeAsync(string path, object body)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync(path, body);
        if (!response.IsSuccessStatusCode)
            throw await CreateApiExceptionAsync(response);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>()
            ?? throw new ApiException((int)response.StatusCode, "Empty response.");

        await tokens.SaveAsync(auth.AccessToken, auth.RefreshToken);
        CurrentUser = auth.User;
        NeedsHandle = auth.NeedsHandle;
    }

    private async Task<UserDto?> FetchMeAsync(string accessToken)
    {
        var client = CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(request);
        if (response.StatusCode == HttpStatusCode.Unauthorized)
            return null;
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserDto>();
    }

    private static async Task<ApiException> CreateApiExceptionAsync(HttpResponseMessage response)
    {
        var detail = "Sign-in failed. Please try again.";
        try
        {
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (json.TryGetProperty("detail", out var value))
            {
                var message = value.GetString();
                if (!string.IsNullOrWhiteSpace(message))
                    detail = message;
            }
        }
        catch
        {
            // Use default message.
        }

        return new ApiException((int)response.StatusCode, detail);
    }
}
