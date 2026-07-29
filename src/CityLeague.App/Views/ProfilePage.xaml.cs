using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class ProfilePage : ContentPage
{
    private readonly ProfileViewModel _vm;
    private bool _didAnimate;

    public ProfilePage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<ProfileViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);

        if (_didAnimate)
            return;

        _didAnimate = true;
        ContentBlock.Opacity = 0;
        ContentBlock.TranslationY = 16;
        GlowOrb.Scale = 0.82;

        await Task.WhenAll(
            ContentBlock.FadeTo(1, 420, Easing.CubicOut),
            ContentBlock.TranslateTo(0, 0, 420, Easing.CubicOut),
            GlowOrb.ScaleTo(1, 720, Easing.CubicOut));
    }
}
