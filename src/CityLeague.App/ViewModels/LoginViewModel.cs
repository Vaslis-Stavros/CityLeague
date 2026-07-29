using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CityLeague.App.Services;
using CityLeague.Core.Validation;

namespace CityLeague.App.ViewModels;

public partial class LoginViewModel(IAuthService auth, ISocialSignInService social) : BaseViewModel
{
    [ObservableProperty]
    private string username = string.Empty;

    [ObservableProperty]
    private string password = string.Empty;

    [ObservableProperty]
    private string email = string.Empty;

    [ObservableProperty]
    private bool isCheckingSession = true;

    [ObservableProperty]
    private bool isSignUpMode;

    [ObservableProperty]
    private bool isGoogleAvailable = true;

    [ObservableProperty]
    private bool isMicrosoftAvailable = true;

    [ObservableProperty]
    private bool isAppleAvailable = true;

    public string ModeHint => IsSignUpMode
        ? "Pick a unique username (3-20 chars, letters, numbers, _). Tap Sign up again to create your account."
        : "Tap Log in again to sign in.";

    partial void OnIsSignUpModeChanged(bool value) => OnPropertyChanged(nameof(ModeHint));

    [RelayCommand]
    private async Task AppearingAsync()
    {
        IsCheckingSession = true;
        try
        {
            if (await auth.LoadSessionAsync())
                await NavigateOnwardAsync();
        }
        catch
        {
            // Ignore; show the login form.
        }
        finally
        {
            IsCheckingSession = false;
        }

        await LoadSocialProvidersAsync();
    }

    private async Task LoadSocialProvidersAsync()
    {
        try
        {
            var options = await social.GetOptionsAsync();
            bool IsUsable(string provider) => options.DevSignInEnabled
                || options.Providers.Any(p => string.Equals(p.Provider, provider, StringComparison.OrdinalIgnoreCase));

            IsGoogleAvailable = IsUsable("google");
            IsMicrosoftAvailable = IsUsable("microsoft");
            IsAppleAvailable = IsUsable("apple");
        }
        catch
        {
            // Server unreachable: keep the buttons and let the attempt surface the error.
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task LoginTabTappedAsync()
    {
        if (IsSignUpMode)
        {
            IsSignUpMode = false;
            ErrorMessage = null;
            return;
        }

        await LoginAsync();
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task SignUpTabTappedAsync()
    {
        if (!IsSignUpMode)
        {
            IsSignUpMode = true;
            ErrorMessage = null;
            return;
        }

        await SignUpAsync();
    }

    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            await ShowValidationErrorAsync("Enter your username.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            await ShowValidationErrorAsync("Enter your password.");
            return;
        }

        await RunAuthAsync(async () =>
        {
            await auth.LoginLocalAsync(Username.Trim(), Password);
            await NavigateOnwardAsync();
        }, "Sign-in failed");
    }

    private async Task SignUpAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        {
            await ShowValidationErrorAsync("Choose a username.");
            return;
        }

        var handle = HandleValidator.Normalize(Username);
        if (!HandleValidator.IsValid(handle, out var reason))
        {
            await ShowValidationErrorAsync(reason ?? "Invalid username.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            await ShowValidationErrorAsync("Choose a password (at least 6 characters).");
            return;
        }

        if (Password.Length < 6)
        {
            await ShowValidationErrorAsync("Password must be at least 6 characters.");
            return;
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            await ShowValidationErrorAsync("Enter your email address.");
            return;
        }

        if (!Email.Contains('@'))
        {
            await ShowValidationErrorAsync("Enter a valid email address.");
            return;
        }

        await RunAuthAsync(async () =>
        {
            await auth.RegisterLocalAsync(handle, Password, Email.Trim());
            await NavigateOnwardAsync();
        }, "Sign-up failed");
    }

    [RelayCommand]
    private async Task SignInSocialAsync(string provider)
    {
        await RunAuthAsync(async () =>
        {
            await auth.LoginSocialAsync(provider);
            await NavigateOnwardAsync();
        }, "Sign-in failed");
    }

    private async Task RunAuthAsync(Func<Task> operation, string alertTitle)
    {
        if (IsBusy) return;

        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await operation();
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = null;
        }
        catch (ApiException ex)
        {
            ErrorMessage = ex.Message;
            await ShowAlertAsync(alertTitle, ex.Message);
        }
        catch (HttpRequestException)
        {
            ErrorMessage =
#if ANDROID
                "Can't reach the server. On the Android emulator use http://10.0.2.2:5066 (not localhost), and make sure the API is running.";
#else
                "Can't reach the server. Check your connection and try again.";
#endif
            await ShowAlertAsync(alertTitle, ErrorMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await ShowAlertAsync(alertTitle, ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowValidationErrorAsync(string message)
    {
        ErrorMessage = message;
        await ShowAlertAsync("Missing information", message);
    }

    private static async Task ShowAlertAsync(string title, string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return;
        var page = Shell.Current?.CurrentPage;
        if (page is not null)
            await page.DisplayAlertAsync(title, message, "OK");
    }

    private async Task NavigateOnwardAsync()
    {
        await Shell.Current.GoToAsync(auth.NeedsHandle ? AppRoutes.Onboarding : AppRoutes.Home);
    }
}
