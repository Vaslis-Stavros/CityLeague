using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class CreateEventPage : ContentPage
{
    private readonly CreateEventViewModel _vm;

    public CreateEventPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<CreateEventViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);
    }
}
