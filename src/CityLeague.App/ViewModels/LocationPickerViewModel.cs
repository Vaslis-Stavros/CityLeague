using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Json;
using CityLeague.App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace CityLeague.App.ViewModels;

public partial class LocationPickerViewModel(IOsmFootballFieldService fields) : BaseViewModel
{
    [ObservableProperty]
    private Location? selectedLocation;

    [ObservableProperty]
    private string? addressPreview;

    [ObservableProperty]
    private string fieldsSubtitle = "Loading football pitches from OpenStreetMap…";

    public ObservableCollection<FootballField> NearbyFields { get; } = [];

    public event EventHandler? FieldsChanged;

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
                    await LoadFieldsAsync(current);
                    return;
                }
            }
        }
        catch
        {
            // Fall back to default below.
        }

        if (SelectedLocation is null)
            SelectedLocation = new Location(37.9838, 23.7275); // Athens default

        await UpdateAddressAsync(SelectedLocation);
        await LoadFieldsAsync(SelectedLocation);
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
            await LoadFieldsAsync(location);
        });
    }

    public async Task SetMapTapAsync(double latitude, double longitude)
    {
        var location = new Location(latitude, longitude);
        SelectedLocation = location;
        await UpdateAddressAsync(location);
    }

    public Task SelectFieldAsync(double latitude, double longitude, string name)
    {
        SelectedLocation = new Location(latitude, longitude);
        var city = fields.CachedCityName;
        AddressPreview = string.IsNullOrWhiteSpace(city) ? name : $"{name}, {city}";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (SelectedLocation is null)
        {
            ErrorMessage = "Pick a location on the map.";
            return;
        }

        if (string.IsNullOrWhiteSpace(AddressPreview))
            await UpdateAddressAsync(SelectedLocation);

        var address = AddressPreview ?? FormatCoordinates(SelectedLocation);
        await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["location"] = address });
    }

    private async Task LoadFieldsAsync(Location location)
    {
        try
        {
            FieldsSubtitle = "Finding football pitches nearby…";
            var list = await fields.FindNearAsync(location.Latitude, location.Longitude);
            NearbyFields.Clear();
            foreach (var f in list)
                NearbyFields.Add(f);

            var city = fields.CachedCityName ?? "your area";
            FieldsSubtitle = NearbyFields.Count == 0
                ? $"No mapped pitches found near {city} yet"
                : $"{NearbyFields.Count} football pitches near {city}";
            FieldsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch
        {
            FieldsSubtitle = "Couldn’t load pitches — you can still tap the map";
        }
    }

    private async Task UpdateAddressAsync(Location location)
    {
        var city = await fields.ResolveCityAsync(location.Latitude, location.Longitude);
        // Prefer a nearby named pitch if the tap is very close to one.
        var nearest = NearbyFields
            .Select(f => (Field: f, Dist: HaversineKm(location.Latitude, location.Longitude, f.Latitude, f.Longitude)))
            .Where(x => x.Dist < 0.08)
            .OrderBy(x => x.Dist)
            .Select(x => x.Field)
            .FirstOrDefault();

        if (nearest is not null)
        {
            AddressPreview = string.IsNullOrWhiteSpace(city) ? nearest.Name : $"{nearest.Name}, {city}";
            return;
        }

        AddressPreview = city is null
            ? FormatCoordinates(location)
            : $"{FormatCoordinates(location)}, {city}";
    }

    public string BuildFieldsPayloadBase64()
    {
        var payload = NearbyFields.Select(f => new
        {
            lat = f.Latitude,
            lng = f.Longitude,
            name = f.Name,
        });
        var json = JsonSerializer.Serialize(payload);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static string FormatCoordinates(Location location)
        => $"{location.Latitude:F5}, {location.Longitude:F5}";

    private static double HaversineKm(double lat1, double lon1, double lat2, double lon2)
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
}
