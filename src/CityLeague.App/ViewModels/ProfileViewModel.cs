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
    public bool ShowEmptyStats => Stats.Count == 0;

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
            OnPropertyChanged(nameof(ShowEmptyStats));
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
                // Copy to memory so the multipart upload has a known length (some platform
                // streams from the photo picker are non-seekable and trip the handler).
                await using var buffer = new MemoryStream();
                await stream.CopyToAsync(buffer);
                buffer.Position = 0;

                var fileName = string.IsNullOrWhiteSpace(photo.FileName) ? "avatar.jpg" : photo.FileName;
                var contentType = string.IsNullOrWhiteSpace(photo.ContentType)
                    ? GuessContentType(fileName)
                    : photo.ContentType;
                var updated = await api.UploadAvatarAsync(buffer, fileName, contentType);
                User = updated;
                auth.UpdateCurrentUser(updated);
                NotifyUser();
            });
        }
        catch (FeatureNotSupportedException)
        {
            ErrorMessage = "Photo picking isn't supported on this device.";
            await ShowAlertAsync(ErrorMessage);
        }
        catch (PermissionException)
        {
            ErrorMessage = "Photo permission was denied.";
            await ShowAlertAsync(ErrorMessage);
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            await ShowAlertAsync(ex.Message);
        }
    }

    private static async Task ShowAlertAsync(string message)
    {
        var page = Shell.Current?.CurrentPage;
        if (page is not null)
            await page.DisplayAlertAsync("Photo", message, "OK");
    }

    private static string GuessContentType(string fileName) => Path.GetExtension(fileName).ToLowerInvariant() switch
    {
        ".png" => "image/png",
        ".gif" => "image/gif",
        ".webp" => "image/webp",
        _ => "image/jpeg",
    };

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
