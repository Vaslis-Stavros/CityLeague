using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Maui.Devices.Sensors;

namespace CityLeague.App.Services;

public sealed record FootballField(
    string Id,
    string Name,
    double Latitude,
    double Longitude,
    string? City,
    string DisplayLabel);

public interface IOsmFootballFieldService
{
    Task<IReadOnlyList<FootballField>> FindNearAsync(
        double latitude, double longitude, double radiusKm = 18, CancellationToken ct = default);

    Task<string?> ResolveCityAsync(double latitude, double longitude, CancellationToken ct = default);

    Task PrefetchForCurrentCityAsync(CancellationToken ct = default);

    IReadOnlyList<FootballField> CachedCityFields { get; }
    string? CachedCityName { get; }
}

/// <summary>
/// Finds football pitches from OpenStreetMap via the public Overpass API
/// (leisure=pitch/stadium + sport=soccer|football). Coverage in Greece is good
/// in cities but incomplete in rural areas — unnamed pitches appear as "Football pitch".
/// </summary>
public sealed class OsmFootballFieldService : IOsmFootballFieldService
{
    public const string HttpClientName = "OsmFootballFields";

    private readonly IHttpClientFactory _httpFactory;
    private readonly object _cacheLock = new();
    private IReadOnlyList<FootballField> _cached = [];
    private string? _cachedCity;
    private double? _cachedLat;
    private double? _cachedLon;

    public OsmFootballFieldService(IHttpClientFactory httpFactory)
        => _httpFactory = httpFactory;

    public IReadOnlyList<FootballField> CachedCityFields
    {
        get { lock (_cacheLock) return _cached; }
    }

    public string? CachedCityName
    {
        get { lock (_cacheLock) return _cachedCity; }
    }

    public async Task PrefetchForCurrentCityAsync(CancellationToken ct = default)
    {
        try
        {
            var location = await Geolocation.Default.GetLastKnownLocationAsync()
                ?? await Geolocation.Default.GetLocationAsync(
                    new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(8)), ct);
            if (location is null)
                location = new Location(37.9838, 23.7275); // Athens fallback

            await FindNearAsync(location.Latitude, location.Longitude, 18, ct);
        }
        catch
        {
            // Autocomplete can stay empty until map/location is used.
        }
    }

    public async Task<IReadOnlyList<FootballField>> FindNearAsync(
        double latitude, double longitude, double radiusKm = 18, CancellationToken ct = default)
    {
        lock (_cacheLock)
        {
            if (_cached.Count > 0
                && _cachedLat is { } clat
                && _cachedLon is { } clon
                && DistanceKm(clat, clon, latitude, longitude) < 4)
                return _cached;
        }

        var city = await ResolveCityAsync(latitude, longitude, ct) ?? "your area";
        var radiusMeters = (int)Math.Clamp(radiusKm * 1000, 2000, 30000);
        var lat = latitude.ToString(CultureInfo.InvariantCulture);
        var lon = longitude.ToString(CultureInfo.InvariantCulture);

        // soccer = FIFA football; football sometimes means American/Aussie — include both for local tagging.
        var query = $"""
            [out:json][timeout:25];
            (
              nwr["leisure"="pitch"]["sport"~"soccer|football",i](around:{radiusMeters},{lat},{lon});
              nwr["leisure"="stadium"]["sport"~"soccer|football",i](around:{radiusMeters},{lat},{lon});
              nwr["leisure"="sports_centre"]["sport"~"soccer|football",i](around:{radiusMeters},{lat},{lon});
            );
            out center tags;
            """;

        var client = _httpFactory.CreateClient(HttpClientName);
        using var content = new StringContent("data=" + Uri.EscapeDataString(query), Encoding.UTF8, "application/x-www-form-urlencoded");
        using var response = await client.PostAsync("https://overpass-api.de/api/interpreter", content, ct);
        if (!response.IsSuccessStatusCode)
            return CachedCityFields;

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var fields = new List<FootballField>();
        if (!doc.RootElement.TryGetProperty("elements", out var elements))
            return fields;

        foreach (var el in elements.EnumerateArray())
        {
            if (!TryReadCenter(el, out var fLat, out var fLon))
                continue;

            var id = $"{el.GetProperty("type").GetString()}:{el.GetProperty("id").GetInt64()}";
            string? name = null;
            if (el.TryGetProperty("tags", out var tags))
            {
                name = ReadTag(tags, "name")
                    ?? ReadTag(tags, "name:en")
                    ?? ReadTag(tags, "name:el")
                    ?? ReadTag(tags, "alt_name");
            }

            name = string.IsNullOrWhiteSpace(name) ? "Football pitch" : name.Trim();
            var label = string.IsNullOrWhiteSpace(city) || city == "your area"
                ? name
                : $"{name} · {city}";

            fields.Add(new FootballField(id, name, fLat, fLon, city, label));
        }

        var ordered = fields
            .GroupBy(f => $"{f.Name}|{f.Latitude:F4}|{f.Longitude:F4}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderBy(f => DistanceKm(latitude, longitude, f.Latitude, f.Longitude))
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .Take(80)
            .ToList();

        lock (_cacheLock)
        {
            _cached = ordered;
            _cachedCity = city;
            _cachedLat = latitude;
            _cachedLon = longitude;
        }

        return ordered;
    }

    public async Task<string?> ResolveCityAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        try
        {
            var client = _httpFactory.CreateClient(HttpClientName);
            var lat = latitude.ToString(CultureInfo.InvariantCulture);
            var lon = longitude.ToString(CultureInfo.InvariantCulture);
            var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={lat}&lon={lon}&zoom=12&addressdetails=1";
            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            if (!doc.RootElement.TryGetProperty("address", out var address))
                return null;

            return ReadTag(address, "city")
                ?? ReadTag(address, "town")
                ?? ReadTag(address, "municipality")
                ?? ReadTag(address, "village")
                ?? ReadTag(address, "suburb")
                ?? ReadTag(address, "county");
        }
        catch
        {
            return null;
        }
    }

    private static bool TryReadCenter(JsonElement el, out double lat, out double lon)
    {
        lat = lon = 0;
        if (el.TryGetProperty("center", out var center)
            && center.TryGetProperty("lat", out var cLat)
            && center.TryGetProperty("lon", out var cLon))
        {
            lat = cLat.GetDouble();
            lon = cLon.GetDouble();
            return true;
        }

        if (el.TryGetProperty("lat", out var nLat) && el.TryGetProperty("lon", out var nLon))
        {
            lat = nLat.GetDouble();
            lon = nLon.GetDouble();
            return true;
        }

        return false;
    }

    private static string? ReadTag(JsonElement tags, string key)
        => tags.TryGetProperty(key, out var value) ? value.GetString() : null;

    private static double DistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6371;
        static double Rad(double d) => d * Math.PI / 180;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2))
                  * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * r * Math.Asin(Math.Sqrt(a));
    }

    public static void ConfigureHttpClient(HttpClient client)
    {
        client.Timeout = TimeSpan.FromSeconds(30);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CityLeague/1.0 (football meetup; OSM Overpass/Nominatim)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }
}
