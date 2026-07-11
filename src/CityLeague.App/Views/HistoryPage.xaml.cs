using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class HistoryPage : ContentPage
{
    private readonly HistoryViewModel _vm;

    public HistoryPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<HistoryViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);
    }
}
