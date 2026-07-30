namespace CityLeague.App.Helpers;

/// <summary>
/// Keeps the system status / notification bar in sync with each screen's backdrop.
/// </summary>
public static class StatusBarTheme
{
    public static readonly Color PitchTop = Color.FromArgb("#06351A");
    public static readonly Color SlateTop = Color.FromArgb("#0E1525");
    public static readonly Color BrandTop = Color.FromArgb("#0B6B2E");

    /// <param name="darkContent">True for dark status-bar icons on a light background.</param>
    public static void Apply(Page page, Color background, bool darkContent = false)
    {
        if (page is null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            page.BackgroundColor = background;
#if ANDROID
            ApplyAndroid(background, darkContent);
#elif IOS
            ApplyIos(darkContent);
#endif
        });
    }

#if ANDROID
    private static void ApplyAndroid(Color background, bool darkContent)
    {
        var activity = Platform.CurrentActivity;
        if (activity?.Window is null) return;

        var window = activity.Window;
        var native = ToAndroidColor(background);

        if (OperatingSystem.IsAndroidVersionAtLeast(21))
        {
            window.AddFlags(Android.Views.WindowManagerFlags.DrawsSystemBarBackgrounds);
            window.ClearFlags(Android.Views.WindowManagerFlags.TranslucentStatus);
#pragma warning disable CA1422
            window.SetStatusBarColor(native);
#pragma warning restore CA1422
        }

        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            var controller = window.InsetsController;
            if (controller is null) return;

            const Android.Views.WindowInsetsControllerAppearance lightBars =
                Android.Views.WindowInsetsControllerAppearance.LightStatusBars;
            controller.SetSystemBarsAppearance(darkContent ? (int)lightBars : 0, (int)lightBars);
        }
        else if (OperatingSystem.IsAndroidVersionAtLeast(23))
        {
#pragma warning disable CA1422
            var decor = window.DecorView;
            if (decor is null) return;
            decor.SystemUiVisibility = darkContent
                ? (Android.Views.StatusBarVisibility)Android.Views.SystemUiFlags.LightStatusBar
                : 0;
#pragma warning restore CA1422
        }
    }

    private static Android.Graphics.Color ToAndroidColor(Color color)
    {
        var a = (int)Math.Round(color.Alpha * 255);
        var r = (int)Math.Round(color.Red * 255);
        var g = (int)Math.Round(color.Green * 255);
        var b = (int)Math.Round(color.Blue * 255);
        return Android.Graphics.Color.Argb(a, r, g, b);
    }
#endif

#if IOS
    private static void ApplyIos(bool darkContent)
    {
        // Page.BackgroundColor paints behind the status bar; style icons for contrast.
        UIKit.UIApplication.SharedApplication.SetStatusBarStyle(
            darkContent ? UIKit.UIStatusBarStyle.DarkContent : UIKit.UIStatusBarStyle.LightContent,
            animated: true);
    }
#endif
}
