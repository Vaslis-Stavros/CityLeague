using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices.Sensors;

namespace CityLeague.App.ViewModels;

public partial class LocationPickerViewModel : BaseViewModel
{
    [ObservableProperty]
    private Location? selectedLocation;

    [ObservableProperty]
    private string? addressPreview;

    [ObservableProperty]
    private bool mapKeyMissing;

    [RelayCommand]
    private async Task AppearingAsync()
    {
#if ANDROID
        MapKeyMissing = IsAndroidMapsApiKeyMissing();
#endif

        try
        {
            var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
            if (status != PermissionStatus.Granted)
                status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();

            if (status == PermissionStatus.Granted)
            {
                var current = await Geolocation.Default.GetLastKnownLocationAsync()
                    ?? await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
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
            SelectedLocation = new Location(37.9838, 23.7275);
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

            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15)));
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
        var address = AddressPreview ?? $"{SelectedLocation.Latitude:F5}, {SelectedLocation.Longitude:F5}";
        await Shell.Current.GoToAsync("..", new Dictionary<string, object> { ["location"] = address });
    }

    private async Task UpdateAddressAsync(Location location)
    {
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
            }
            else
            {
                AddressPreview = FormatCoordinates(location);
            }
        }
        catch
        {
            AddressPreview = FormatCoordinates(location);
        }
    }

    private static string FormatCoordinates(Location location)
        => $"{location.Latitude:F5}, {location.Longitude:F5}";

#if ANDROID
    private static bool IsAndroidMapsApiKeyMissing()
    {
        try
        {
            var context = global::Android.App.Application.Context;
            var appInfo = context.PackageManager?.GetApplicationInfo(
                context.PackageName!,
                global::Android.Content.PM.PackageInfoFlags.MetaData);
            var key = appInfo?.MetaData?.GetString("com.google.android.geo.API_KEY");
            return string.IsNullOrWhiteSpace(key)
                   || key.Contains("YOUR_GOOGLE", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return true;
        }
    }
#endif
}
