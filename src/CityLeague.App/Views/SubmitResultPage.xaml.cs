using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class SubmitResultPage : ContentPage
{
    private readonly SubmitResultViewModel _vm;

    public SubmitResultPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<SubmitResultViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);
    }
}
