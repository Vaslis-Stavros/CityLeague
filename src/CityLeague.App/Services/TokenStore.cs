using Microsoft.Maui.Storage;

namespace CityLeague.App.Services;

/// <summary>Persists auth tokens in secure storage with an in-memory cache for sync access.</summary>
public interface ITokenStore
{
    string? AccessToken { get; }
    string? RefreshToken { get; }
    bool HasTokens { get; }

    Task LoadAsync();
    Task SaveAsync(string accessToken, string refreshToken);
    Task ClearAsync();
}

public class TokenStore : ITokenStore
{
    private const string AccessKey = "cmi_access_token";
    private const string RefreshKey = "cmi_refresh_token";

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public bool HasTokens => !string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(RefreshToken);

    public async Task LoadAsync()
    {
        try
        {
            AccessToken = await SecureStorage.Default.GetAsync(AccessKey);
            RefreshToken = await SecureStorage.Default.GetAsync(RefreshKey);
        }
        catch
        {
            AccessToken = null;
            RefreshToken = null;
        }
    }

    public async Task SaveAsync(string accessToken, string refreshToken)
    {
        AccessToken = accessToken;
        RefreshToken = refreshToken;
        try
        {
            await SecureStorage.Default.SetAsync(AccessKey, accessToken);
            await SecureStorage.Default.SetAsync(RefreshKey, refreshToken);
        }
        catch
        {
            // Secure storage can be unavailable on some emulators; in-memory still works for the session.
        }
    }

    public Task ClearAsync()
    {
        AccessToken = null;
        RefreshToken = null;
        SecureStorage.Default.Remove(AccessKey);
        SecureStorage.Default.Remove(RefreshKey);
        return Task.CompletedTask;
    }
}
