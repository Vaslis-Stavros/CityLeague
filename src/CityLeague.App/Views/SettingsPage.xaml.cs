using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class SettingsPage : ContentPage
{
    private readonly SettingsViewModel _vm;

    public SettingsPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<SettingsViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StatusBarTheme.Apply(this, ScreenChrome.Slate);
        _vm.AppearingCommand.Execute(null);
    }
}
