using System.Globalization;
using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class LocationPickerPage : ContentPage
{
    private readonly LocationPickerViewModel _vm;
    private bool _mapReady;

    public LocationPickerPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<LocationPickerViewModel>();
        BindingContext = _vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LocationPickerViewModel.SelectedLocation))
                _ = SyncMarkerAsync();
        };
        _vm.FieldsChanged += (_, _) => _ = SyncFieldsAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadMapHtmlAsync();
        _vm.AppearingCommand.Execute(null);
    }

    private async Task LoadMapHtmlAsync()
    {
        await using var stream = await FileSystem.OpenAppPackageFileAsync("location_picker.html");
        using var reader = new StreamReader(stream);
        var html = await reader.ReadToEndAsync();
        MapWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private async void OnMapNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url is null)
            return;

        if (!e.Url.Contains("cityleague.app/bridge/", StringComparison.OrdinalIgnoreCase))
            return;

        e.Cancel = true;

        if (e.Url.Contains("/bridge/mapready", StringComparison.OrdinalIgnoreCase))
        {
            _mapReady = true;
            await SyncMarkerAsync(forceZoom: true);
            await SyncFieldsAsync();
            return;
        }

        if (!Uri.TryCreate(e.Url, UriKind.Absolute, out var uri))
            return;

        var query = ParseQuery(uri.Query);

        if (e.Url.Contains("/bridge/field", StringComparison.OrdinalIgnoreCase))
        {
            if (!TryParseLatLng(query, out var flat, out var flng))
                return;
            query.TryGetValue("name", out var name);
            await _vm.SelectFieldAsync(flat, flng, string.IsNullOrWhiteSpace(name) ? "Football pitch" : name);
            return;
        }

        if (!e.Url.Contains("/bridge/maptap", StringComparison.OrdinalIgnoreCase))
            return;

        if (!TryParseLatLng(query, out var lat, out var lng))
            return;

        await _vm.SetMapTapAsync(lat, lng);
    }

    private async Task SyncMarkerAsync(bool forceZoom = false)
    {
        if (!_mapReady || _vm.SelectedLocation is not { } loc)
            return;

        var lat = loc.Latitude.ToString(CultureInfo.InvariantCulture);
        var lng = loc.Longitude.ToString(CultureInfo.InvariantCulture);
        var zoom = forceZoom ? "14" : "0";
        try
        {
            await MapWebView.EvaluateJavaScriptAsync($"setMarker({lat},{lng},{zoom})");
        }
        catch
        {
            // WebView may not be ready yet.
        }
    }

    private async Task SyncFieldsAsync()
    {
        if (!_mapReady || _vm.NearbyFields.Count == 0)
            return;

        try
        {
            var b64 = _vm.BuildFieldsPayloadBase64();
            await MapWebView.EvaluateJavaScriptAsync($"setFootballFieldsB64('{b64}')");
        }
        catch
        {
            // Retry on next FieldsChanged.
        }
    }

    private static bool TryParseLatLng(Dictionary<string, string> query, out double lat, out double lng)
    {
        lat = lng = 0;
        return query.TryGetValue("lat", out var latText)
               && query.TryGetValue("lng", out var lngText)
               && double.TryParse(latText, NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
               && double.TryParse(lngText, NumberStyles.Float, CultureInfo.InvariantCulture, out lng);
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
            return result;

        var trimmed = query.TrimStart('?');
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pieces = part.Split('=', 2);
            if (pieces.Length == 0) continue;
            var key = Uri.UnescapeDataString(pieces[0]);
            var value = pieces.Length > 1 ? Uri.UnescapeDataString(pieces[1]) : string.Empty;
            result[key] = value;
        }

        return result;
    }
}
