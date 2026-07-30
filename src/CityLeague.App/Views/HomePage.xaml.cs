using CityLeague.App.Helpers;
using CityLeague.App.Services;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;
    private readonly IAppPreferences _prefs;
    private bool _didAnimate;
    private string? _appliedSportKey;
    private bool _appliedLight;
    private bool _themeBusy;

    public HomePage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<HomeViewModel>();
        _prefs = ServiceHelper.GetService<IAppPreferences>();
        BindingContext = _vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(HomeViewModel.SelectedSport)
                or nameof(HomeViewModel.BackdropTop)
                or nameof(HomeViewModel.AccentColor)
                or nameof(HomeViewModel.SoftTextColor))
                _ = ApplySportThemeAsync(animate: true);
        };
        _prefs.Changed += OnPrefsChanged;
    }

    private void OnPrefsChanged(object? sender, EventArgs e)
        => MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _appliedSportKey = null;
                _vm.NotifyThemeChanged();
                _ = ApplySportThemeAsync(animate: false);
            }
            catch
            {
                // Theme refresh must not crash a background tab.
            }
        });

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);
        await ApplySportThemeAsync(animate: false);

        if (_didAnimate)
            return;

        _didAnimate = true;
        HeaderBlock.Opacity = 0;
        HeaderBlock.TranslationY = 14;
        CreateCta.TranslationY = 20;
        GlowOrb.Scale = 0.82;

        await Task.WhenAll(
            HeaderBlock.FadeTo(1, 420, Easing.CubicOut),
            HeaderBlock.TranslateTo(0, 0, 420, Easing.CubicOut),
            GlowOrb.ScaleTo(1, 720, Easing.CubicOut),
            CreateCta.TranslateTo(0, 0, 480, Easing.CubicOut));
    }

    private async Task ApplySportThemeAsync(bool animate)
    {
        if (_themeBusy) return;
        _themeBusy = true;
        try
        {
            var key = _vm.SelectedSport?.Key ?? "football";
            var light = _prefs.IsLight;
            if (animate
                && string.Equals(key, _appliedSportKey, StringComparison.OrdinalIgnoreCase)
                && light == _appliedLight)
                return;

            _appliedSportKey = key;
            _appliedLight = light;
            var theme = SportColors.GetTheme(key, light);

            // Never animate theme toggles — FadeTo while off-tab is a crash source.
            if (animate && IsLoaded && Window is not null)
            {
                await Backdrop.FadeTo(0.55, 120, Easing.CubicIn);
                ApplyTheme(theme, light);
                _ = GlowOrb.ScaleTo(0.9, 120, Easing.CubicIn);
                await Task.WhenAll(
                    Backdrop.FadeTo(1, 220, Easing.CubicOut),
                    GlowOrb.ScaleTo(1, 320, Easing.CubicOut));
            }
            else
            {
                ApplyTheme(theme, light);
            }
        }
        catch
        {
            // Keep home usable even if a sport-theme transition fails.
        }
        finally
        {
            _themeBusy = false;
        }
    }

    private void ApplyTheme(SportColors.BackdropTheme theme, bool light)
    {
        StopTop.Color = theme.Top;
        StopMid.Color = theme.Mid;
        StopBottom.Color = theme.Bottom;
        GlowOrb.Fill = theme.Glow;
        SubtitleLabel.TextColor = theme.SoftMuted;
        BackgroundColor = theme.Top;
        StatusBarTheme.Apply(this, theme.Top, darkContent: light);
    }
}
