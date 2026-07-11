using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Validation;

namespace CityLeague.App.ViewModels;

public partial class OnboardingHandleViewModel(ICityLeagueApi api, IAuthService auth) : BaseViewModel
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHint))]
    private string handle = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHint))]
    private string? hint;

    [ObservableProperty]
    private bool isAvailable;

    public bool HasHint => !string.IsNullOrEmpty(Hint);

    partial void OnHandleChanged(string value)
    {
        IsAvailable = false;
        if (string.IsNullOrWhiteSpace(value))
        {
            Hint = null;
            return;
        }
        Hint = HandleValidator.IsValid(value, out var reason) ? null : reason;
    }

    [RelayCommand]
    private async Task CheckAsync()
    {
        var normalized = HandleValidator.Normalize(Handle);
        if (!HandleValidator.IsValid(normalized, out var reason))
        {
            Hint = reason;
            IsAvailable = false;
            return;
        }

        await RunAsync(async () =>
        {
            var result = await api.CheckHandleAsync(normalized);
            IsAvailable = result.Available;
            Hint = result.Available ? "Available!" : result.Reason ?? "That handle is taken.";
        });
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var normalized = HandleValidator.Normalize(Handle);
        if (!HandleValidator.IsValid(normalized, out var reason))
        {
            Hint = reason;
            return;
        }

        await RunAsync(async () =>
        {
            var user = await api.SetHandleAsync(normalized);
            auth.UpdateCurrentUser(user);
            await Shell.Current.GoToAsync(AppRoutes.Home);
        });
    }
}
