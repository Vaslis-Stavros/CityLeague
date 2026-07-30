using CityLeague.App.Helpers;
using CityLeague.App.ViewModels;

namespace CityLeague.App.Views;

public partial class LoginPage : ContentPage
{
    private readonly LoginViewModel _vm;

    public LoginPage()
    {
        InitializeComponent();
        _vm = ServiceHelper.GetService<LoginViewModel>();
        BindingContext = _vm;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        StatusBarTheme.Apply(this, ScreenChrome.Brand);
        _vm.AppearingCommand.Execute(null);
    }

    private async void OnLoginTabClicked(object? sender, EventArgs e)
    {
        CommitForm();
        await _vm.LoginTabTappedCommand.ExecuteAsync(null);
    }

    private async void OnSignUpTabClicked(object? sender, EventArgs e)
    {
        CommitForm();
        await _vm.SignUpTabTappedCommand.ExecuteAsync(null);
    }

    private void CommitForm()
    {
        UsernameEntry.Unfocus();
        EmailEntry.Unfocus();
        PasswordEntry.Unfocus();

        _vm.Username = UsernameEntry.Text ?? string.Empty;
        _vm.Email = EmailEntry.Text ?? string.Empty;
        _vm.Password = PasswordEntry.Text ?? string.Empty;
    }
}
