using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class ContactsPage : ContentPage
{
    private readonly ContactsViewModel _vm;
    private bool _didAnimate;

    public ContactsPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<ContactsViewModel>();
        BindingContext = _vm;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StatusBarTheme.Apply(this, ScreenChrome.Slate);
        _vm.AppearingCommand.Execute(null);

        if (_didAnimate)
            return;

        _didAnimate = true;
        HeaderBlock.Opacity = 0;
        HeaderBlock.TranslationY = 14;
        SearchBlock.Opacity = 0;
        SearchBlock.TranslationY = 12;
        ListBlock.Opacity = 0;
        ListBlock.TranslationY = 16;
        GlowOrb.Scale = 0.82;

        await Task.WhenAll(
            HeaderBlock.FadeTo(1, 400, Easing.CubicOut),
            HeaderBlock.TranslateTo(0, 0, 400, Easing.CubicOut),
            SearchBlock.FadeTo(1, 460, Easing.CubicOut),
            SearchBlock.TranslateTo(0, 0, 460, Easing.CubicOut),
            ListBlock.FadeTo(1, 520, Easing.CubicOut),
            ListBlock.TranslateTo(0, 0, 520, Easing.CubicOut),
            GlowOrb.ScaleTo(1, 720, Easing.CubicOut));
    }
}
