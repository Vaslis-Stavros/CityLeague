using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _vm;
    private bool _didAnimate;

    public HomePage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<HomeViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);

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
}
