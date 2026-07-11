using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class LeaguesPage : ContentPage
{
    private readonly LeaguesViewModel _vm;

    public LeaguesPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<LeaguesViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);
    }
}
