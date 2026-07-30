using CityLeague.App.Helpers;
using CityLeague.App.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CityLeague.App.Controls;

/// <summary>Theme-colored circular checkbox for invite multi-select lists.</summary>
public class RoundCheckBox : ContentView
{
    public static readonly BindableProperty IsCheckedProperty =
        BindableProperty.Create(
            nameof(IsChecked),
            typeof(bool),
            typeof(RoundCheckBox),
            false,
            BindingMode.TwoWay,
            propertyChanged: static (b, _, _) => ((RoundCheckBox)b).Refresh());

    private readonly Border _ring;
    private readonly Label _mark;

    public RoundCheckBox()
    {
        WidthRequest = 28;
        HeightRequest = 28;
        VerticalOptions = LayoutOptions.Center;
        HorizontalOptions = LayoutOptions.Center;

        _mark = new Label
        {
            Text = "✓",
            FontFamily = "OutfitSemiBold",
            FontSize = 14,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false,
        };

        _ring = new Border
        {
            WidthRequest = 26,
            HeightRequest = 26,
            StrokeThickness = 2,
            Stroke = Colors.White,
            BackgroundColor = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 13 },
            Padding = 0,
            Content = _mark,
        };

        Content = _ring;

        var tap = new TapGestureRecognizer();
        tap.Tapped += (_, _) => IsChecked = !IsChecked;
        GestureRecognizers.Add(tap);
        Refresh();
    }

    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    private void Refresh()
    {
        var accent = ResolveAccent();
        if (IsChecked)
        {
            _ring.BackgroundColor = accent;
            _ring.Stroke = accent;
            _mark.IsVisible = true;
        }
        else
        {
            _ring.BackgroundColor = Colors.Transparent;
            _ring.Stroke = Color.FromArgb("#AAFFFFFF");
            _mark.IsVisible = false;
        }
    }

    private static Color ResolveAccent()
    {
        try
        {
            var prefs = ServiceHelper.GetService<IAppPreferences>();
            return prefs.IsLight ? Color.FromArgb("#0B6B2E") : Color.FromArgb("#F2A900");
        }
        catch
        {
            return Color.FromArgb("#F2A900");
        }
    }
}
