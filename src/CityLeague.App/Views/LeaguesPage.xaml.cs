using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class LeaguesPage : ContentPage
{
    private readonly LeaguesViewModel _vm;
    private bool _didAnimate;

    public LeaguesPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<LeaguesViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StatusBarTheme.Apply(this, StatusBarTheme.PitchTop);
        _vm.AppearingCommand.Execute(null);

        if (_didAnimate)
            return;

        _didAnimate = true;
        HeaderBlock.Opacity = 0;
        HeaderBlock.TranslationY = 14;
        ListBlock.Opacity = 0;
        ListBlock.TranslationY = 16;
        CreateCta.TranslationY = 16;
        GlowOrb.Scale = 0.82;

        await Task.WhenAll(
            HeaderBlock.FadeTo(1, 400, Easing.CubicOut),
            HeaderBlock.TranslateTo(0, 0, 400, Easing.CubicOut),
            CreateCta.TranslateTo(0, 0, 460, Easing.CubicOut),
            ListBlock.FadeTo(1, 500, Easing.CubicOut),
            ListBlock.TranslateTo(0, 0, 500, Easing.CubicOut),
            GlowOrb.ScaleTo(1, 720, Easing.CubicOut));
    }
}
