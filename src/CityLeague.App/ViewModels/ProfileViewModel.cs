using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Dtos;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace CityLeague.App.ViewModels;

public partial class ProfileViewModel(ICityLeagueApi api, IAuthService auth) : BaseViewModel
{
    public ObservableCollection<PlayerStatsDto> Stats { get; } = [];

    [ObservableProperty]
    private UserDto? user;

    [ObservableProperty]
    private bool isRefreshing;

    public string DisplayName => User?.DisplayName ?? "";
    public string HandleText => User?.Handle is { } h ? $"@{h}" : "";
    public string? AvatarUrl => User?.AvatarUrl;

    [RelayCommand]
    private async Task AppearingAsync() => await LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        await RunAsync(async () =>
        {
            var stats = await api.GetMyStatsAsync();
            User = stats.User;
            auth.UpdateCurrentUser(stats.User);
            Stats.Clear();
            foreach (var s in stats.Stats)
                Stats.Add(s);
            NotifyUser();
        });
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        IsRefreshing = true;
        try { await LoadAsync(); }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private async Task ChangePhotoAsync()
    {
        try
        {
            var photo = await MediaPicker.Default.PickPhotoAsync();
            if (photo is null) return;

            await RunAsync(async () =>
            {
                await using var stream = await photo.OpenReadAsync();
                var contentType = string.IsNullOrEmpty(photo.ContentType) ? "image/jpeg" : photo.ContentType;
                var updated = await api.UploadAvatarAsync(stream, photo.FileName, contentType);
                User = updated;
                auth.UpdateCurrentUser(updated);
                NotifyUser();
            });
        }
        catch (FeatureNotSupportedException)
        {
            ErrorMessage = "Photo picking isn't supported on this device.";
        }
        catch (PermissionException)
        {
            ErrorMessage = "Photo permission was denied.";
        }
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await auth.LogoutAsync();
        await Shell.Current.GoToAsync(AppRoutes.Login);
    }

    private void NotifyUser()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(HandleText));
        OnPropertyChanged(nameof(AvatarUrl));
    }
}
