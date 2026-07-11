using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Maps;

namespace CityLeague.App.Views;

public partial class LocationPickerPage : ContentPage
{
    private readonly LocationPickerViewModel _vm;
    private Pin? _pin;

    public LocationPickerPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<LocationPickerViewModel>();
        BindingContext = _vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LocationPickerViewModel.SelectedLocation))
                UpdateMap();
        };
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        MapView.MapType = MapType.Street;
        _vm.AppearingCommand.Execute(null);
        UpdateMap();
    }

    private async void OnMapClicked(object? sender, MapClickedEventArgs e)
    {
        await _vm.SetMapTapAsync(e.Location.Latitude, e.Location.Longitude);
    }

    private void UpdateMap()
    {
        if (_vm.SelectedLocation is not { } loc)
            return;

        var center = new Location(loc.Latitude, loc.Longitude);
        MapView.MoveToRegion(MapSpan.FromCenterAndRadius(center, Distance.FromKilometers(2)));

        if (_pin is not null)
            MapView.Pins.Remove(_pin);

        _pin = new Pin
        {
            Label = "Pitch",
            Location = center,
            Type = PinType.Place,
        };
        MapView.Pins.Add(_pin);
    }
}
