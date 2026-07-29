namespace CityLeague.App.Services;

/// <summary>Client configuration for reaching the API.</summary>
public class ApiSettings
{
    private string _baseUrl = DefaultBaseUrl();

    /// <summary>
    /// Base URL of the API. In DEBUG this defaults to the local API:
    /// Android emulator → http://10.0.2.2:5066 (host machine loopback),
    /// iOS simulator / other → http://localhost:5066.
    /// Release builds default to production.
    /// Assignments of localhost/127.0.0.1 are rewritten automatically on Android.
    /// </summary>
    public string BaseUrl
    {
        get => _baseUrl;
        set => _baseUrl = NormalizeLocalUrl(value);
    }

    public string HubUrl => $"{BaseUrl.TrimEnd('/')}/hubs/events";

    private static string DefaultBaseUrl()
    {
#if DEBUG
        return NormalizeLocalUrl("http://localhost:5066");
#else
        return "https://cityleagueapp.com";
#endif
    }

    /// <summary>
    /// On Android, localhost/127.0.0.1 is the emulator itself — not your PC. Map those
    /// hosts to 10.0.2.2 so a BaseUrl typed as http://localhost:5066 still works.
    /// Physical devices should use your machine's LAN IP instead (e.g. http://192.168.x.x:5066).
    /// </summary>
    public static string NormalizeLocalUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return url;

        if (!Uri.TryCreate(url.Trim().TrimEnd('/'), UriKind.Absolute, out var uri))
            return url.Trim();

#if ANDROID
        if (IsLoopback(uri.Host))
        {
            var builder = new UriBuilder(uri)
            {
                Host = "10.0.2.2",
                // Keep an explicit port so UriBuilder doesn't drop :5066.
                Port = uri.IsDefaultPort ? -1 : uri.Port,
            };
            return builder.Uri.GetLeftPart(UriPartial.Authority)
                + builder.Uri.PathAndQuery.TrimEnd('/');
        }
#endif
        return uri.GetLeftPart(UriPartial.Authority) + uri.PathAndQuery.TrimEnd('/');
    }

    private static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || host is "127.0.0.1" or "::1";
}
