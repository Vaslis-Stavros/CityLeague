using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class MorePage : ContentPage
{
    private readonly MoreViewModel _vm;
    private bool _didAnimate;

    public MorePage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<MoreViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StatusBarTheme.Apply(this, ScreenChrome.Slate);

        if (_didAnimate)
            return;

        _didAnimate = true;
        HeaderBlock.Opacity = 0;
        HeaderBlock.TranslationY = 14;
        MenuBlock.Opacity = 0;
        MenuBlock.TranslationY = 18;
        HistoryCard.TranslationY = 10;
        ProfileCard.TranslationY = 16;
        SettingsCard.TranslationY = 22;
        GlowOrb.Scale = 0.82;

        await Task.WhenAll(
            HeaderBlock.FadeTo(1, 380, Easing.CubicOut),
            HeaderBlock.TranslateTo(0, 0, 380, Easing.CubicOut),
            MenuBlock.FadeTo(1, 460, Easing.CubicOut),
            MenuBlock.TranslateTo(0, 0, 460, Easing.CubicOut),
            HistoryCard.TranslateTo(0, 0, 480, Easing.CubicOut),
            ProfileCard.TranslateTo(0, 0, 540, Easing.CubicOut),
            SettingsCard.TranslateTo(0, 0, 600, Easing.CubicOut),
            GlowOrb.ScaleTo(1, 720, Easing.CubicOut));
    }
}
