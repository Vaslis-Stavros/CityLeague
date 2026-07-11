namespace CityLeague.App.Services;

/// <summary>Client configuration for reaching the API.</summary>
public class ApiSettings
{
    /// <summary>
    /// Base URL of the API. Defaults to the Android emulator loopback alias (10.0.2.2)
    /// which maps to the host machine's localhost. Override for devices/production.
    /// </summary>
    public string BaseUrl { get; set; } = "https://cityleagueapp.com";

    public string HubUrl => $"{BaseUrl.TrimEnd('/')}/hubs/events";
}
