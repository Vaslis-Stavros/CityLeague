using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class CreateEventPage : ContentPage
{
    private readonly CreateEventViewModel _vm;
    private bool _didAnimate;

    public CreateEventPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<CreateEventViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StatusBarTheme.Apply(this, ScreenChrome.Pitch);
        _vm.AppearingCommand.Execute(null);

        if (_didAnimate)
            return;

        _didAnimate = true;
        HeaderBlock.Opacity = 0;
        HeaderBlock.TranslationY = 14;
        FormBlock.Opacity = 0;
        FormBlock.TranslationY = 16;
        CreateCta.TranslationY = 20;
        GlowOrb.Scale = 0.82;

        await Task.WhenAll(
            HeaderBlock.FadeTo(1, 400, Easing.CubicOut),
            HeaderBlock.TranslateTo(0, 0, 400, Easing.CubicOut),
            FormBlock.FadeTo(1, 480, Easing.CubicOut),
            FormBlock.TranslateTo(0, 0, 480, Easing.CubicOut),
            GlowOrb.ScaleTo(1, 720, Easing.CubicOut),
            CreateCta.TranslateTo(0, 0, 480, Easing.CubicOut));
    }
}
