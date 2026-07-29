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

    [ObservableProperty]
    private bool showEditName;

    [ObservableProperty]
    private bool showChangePassword;

    [ObservableProperty]
    private string editDisplayName = string.Empty;

    [ObservableProperty]
    private string currentPassword = string.Empty;

    [ObservableProperty]
    private string newPassword = string.Empty;

    [ObservableProperty]
    private string confirmPassword = string.Empty;

    public string DisplayName => User?.DisplayName ?? "";
    public string HandleText => User?.Handle is { } h ? $"@{h}" : "";
    public string? AvatarUrl => User?.AvatarUrl;
    public bool ShowEmptyStats => Stats.Count == 0;
    public bool HasPassword => User?.HasPassword ?? false;
    public string PasswordSectionTitle => HasPassword ? "Change password" : "Set a password";

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
    private void ToggleEditName()
    {
        ShowEditName = !ShowEditName;
        if (ShowEditName)
            EditDisplayName = DisplayName;
        ErrorMessage = null;
    }

    [RelayCommand]
    private void ToggleChangePassword()
    {
        ShowChangePassword = !ShowChangePassword;
        CurrentPassword = string.Empty;
        NewPassword = string.Empty;
        ConfirmPassword = string.Empty;
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SaveNameAsync()
    {
        var name = EditDisplayName?.Trim() ?? string.Empty;
        if (name.Length < 2)
        {
            ErrorMessage = "Name needs at least 2 characters.";
            return;
        }

        await RunAsync(async () =>
        {
            var updated = await api.UpdateProfileAsync(new UpdateProfileRequest(name, null));
            User = updated;
            auth.UpdateCurrentUser(updated);
            NotifyUser();
            ShowEditName = false;
        });
    }

    [RelayCommand]
    private async Task SavePasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
        {
            ErrorMessage = "New password must be at least 6 characters.";
            return;
        }

        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            ErrorMessage = "New passwords don’t match.";
            return;
        }

        if (HasPassword && string.IsNullOrWhiteSpace(CurrentPassword))
        {
            ErrorMessage = "Enter your current password.";
            return;
        }

        await RunAsync(async () =>
        {
            var updated = await api.ChangePasswordAsync(
                new ChangePasswordRequest(HasPassword ? CurrentPassword : null, NewPassword));
            User = updated;
            auth.UpdateCurrentUser(updated);
            NotifyUser();
            ShowChangePassword = false;
            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmPassword = string.Empty;
        });
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
            ErrorMessage = "Photo picking isn’t supported on this device.";
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
        OnPropertyChanged(nameof(HasPassword));
        OnPropertyChanged(nameof(PasswordSectionTitle));
    }
}
