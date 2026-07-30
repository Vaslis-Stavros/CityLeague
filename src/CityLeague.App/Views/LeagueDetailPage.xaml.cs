using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class LeagueDetailPage : ContentPage
{
    private readonly LeagueDetailViewModel _vm;
    private bool _didAnimate;

    public LeagueDetailPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<LeagueDetailViewModel>();
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
        ContentBlock.Opacity = 0;
        ContentBlock.TranslationY = 16;
        GlowOrb.Scale = 0.82;

        await Task.WhenAll(
            ContentBlock.FadeTo(1, 420, Easing.CubicOut),
            ContentBlock.TranslateTo(0, 0, 420, Easing.CubicOut),
            GlowOrb.ScaleTo(1, 720, Easing.CubicOut));
    }
}
