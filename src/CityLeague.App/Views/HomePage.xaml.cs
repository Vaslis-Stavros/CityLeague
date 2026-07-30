using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;
    private bool _didAnimate;
    private string? _appliedSportKey;

    public HomePage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<HomeViewModel>();
        BindingContext = _vm;
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(HomeViewModel.SelectedSport)
                or nameof(HomeViewModel.BackdropTop)
                or nameof(HomeViewModel.AccentColor))
                _ = ApplySportThemeAsync(animate: true);
        };
    }

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
        var key = _vm.SelectedSport?.Key ?? "football";
        if (animate && string.Equals(key, _appliedSportKey, StringComparison.OrdinalIgnoreCase))
            return;

        _appliedSportKey = key;
        var theme = SportColors.GetTheme(key);

        if (animate)
        {
            await Backdrop.FadeTo(0.55, 120, Easing.CubicIn);
            ApplyTheme(theme);
            _ = GlowOrb.ScaleTo(0.9, 120, Easing.CubicIn);
            await Task.WhenAll(
                Backdrop.FadeTo(1, 220, Easing.CubicOut),
                GlowOrb.ScaleTo(1, 320, Easing.CubicOut));
        }
        else
        {
            ApplyTheme(theme);
        }
    }

    private void ApplyTheme(SportColors.BackdropTheme theme)
    {
        StopTop.Color = theme.Top;
        StopMid.Color = theme.Mid;
        StopBottom.Color = theme.Bottom;
        GlowOrb.Fill = theme.Glow;
        BrandLabel.TextColor = theme.SoftText;
        SubtitleLabel.TextColor = theme.SoftMuted;
        StatusBarTheme.Apply(this, theme.Top);
    }
}
