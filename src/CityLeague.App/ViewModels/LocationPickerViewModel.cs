using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace CityLeague.App.ViewModels;

public partial class LocationPickerViewModel : BaseViewModel
{
    private static readonly HttpClient Nominatim = CreateNominatimClient();

    [ObservableProperty]
    private Location? selectedLocation;

    [ObservableProperty]
    private string? addressPreview;

    [RelayCommand]
    private async Task AppearingAsync()
    {
        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (status == PermissionStatus.Granted)
            {
                var current = await Geolocation.Default.GetLastKnownLocationAsync()
                    ?? await Geolocation.Default.GetLocationAsync(
                        new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
                if (current is not null)
                {
                    SelectedLocation = current;
                    await UpdateAddressAsync(current);
                    return;
                }
            }
        }
        catch
        {
            // Fall back to default below.
        }

        if (SelectedLocation is null)
        {
            SelectedLocation = new Location(37.9838, 23.7275); // Athens default
            await UpdateAddressAsync(SelectedLocation);
        }
    }

    [RelayCommand]
    private async Task UseMyLocationAsync()
    {
        await RunAsync(async () =>
        {
            var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
            {
                ErrorMessage = "Location permission is required.";
                return;
            }

            var location = await Geolocation.Default.GetLocationAsync(
                new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15)));
            if (location is null)
            {
                ErrorMessage = "Could not get your location.";
                return;
            }

            SelectedLocation = location;
            await UpdateAddressAsync(location);
        });
    }

    public async Task SetMapTapAsync(double latitude, double longitude)
    {
        var location = new Location(latitude, longitude);
        SelectedLocation = location;
        await UpdateAddressAsync(location);
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (SelectedLocation is null)
        {
            ErrorMessage = "Pick a location on the map.";
            return;
        }

        await UpdateAddressAsync(SelectedLocation);
        var address = AddressPreview ?? FormatCoordinates(SelectedLocation);
        await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["location"] = address });
    }

    private async Task UpdateAddressAsync(Location location)
    {
        var fromOsm = await ReverseGeocodeNominatimAsync(location);
        if (!string.IsNullOrWhiteSpace(fromOsm))
        {
            AddressPreview = fromOsm;
            return;
        }

        // Platform geocoder as a secondary option (may require vendor keys on some devices).
        try
        {
            var placemarks = await Geocoding.Default.GetPlacemarksAsync(location);
            var place = placemarks?.FirstOrDefault();
            if (place is not null)
            {
                var parts = new[] { place.FeatureName, place.Thoroughfare, place.Locality, place.AdminArea }
                    .Where(p => !string.IsNullOrWhiteSpace(p))
                    .Distinct();
                AddressPreview = string.Join(", ", parts);
                if (!string.IsNullOrWhiteSpace(AddressPreview))
                    return;
            }
        }
        catch
        {
            // Coordinates fallback below.
        }

        AddressPreview = FormatCoordinates(location);
    }

    private static async Task<string?> ReverseGeocodeNominatimAsync(Location location)
    {
        try
        {
            var lat = location.Latitude.ToString(CultureInfo.InvariantCulture);
            var lon = location.Longitude.ToString(CultureInfo.InvariantCulture);
            var url = $"https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={lat}&lon={lon}";
            using var response = await Nominatim.GetAsync(url);
            if (!response.IsSuccessStatusCode)
                return null;

            await using var stream = await response.Content.ReadAsStreamAsync();
            using var doc = await JsonDocument.ParseAsync(stream);
            if (doc.RootElement.TryGetProperty("display_name", out var display)
                && display.GetString() is { Length: > 0 } name)
            {
                // Keep the preview short for the glass panel.
                var parts = name.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                return string.Join(", ", parts.Take(4));
            }
        }
        catch
        {
            // Offline / rate-limit — fall through.
        }

        return null;
    }

    private static string FormatCoordinates(Location location)
        => $"{location.Latitude:F5}, {location.Longitude:F5}";

    private static HttpClient CreateNominatimClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
        // Nominatim usage policy requires a identifying User-Agent.
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CityLeague/1.0 (local football meetup app)");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }
}
