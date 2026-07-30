using CommunityToolkit.Mvvm.Input;

namespace CityLeague.App.ViewModels;

public partial class MoreViewModel : BaseViewModel
{
    [RelayCommand]
    private async Task OpenHistoryAsync()
        => await Shell.Current.GoToAsync(AppRoutes.History);

    [RelayCommand]
    private async Task OpenProfileAsync()
        => await Shell.Current.GoToAsync(AppRoutes.Profile);
}
