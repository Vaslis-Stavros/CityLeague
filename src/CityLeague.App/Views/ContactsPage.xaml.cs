using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class ContactsPage : ContentPage
{
    private readonly ContactsViewModel _vm;

    public ContactsPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<ContactsViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _vm.AppearingCommand.Execute(null);
    }
}
