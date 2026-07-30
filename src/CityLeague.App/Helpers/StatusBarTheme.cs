using CityLeague.App.Services;

namespace CityLeague.App.Helpers;

/// <summary>
/// Keeps the system status / notification bar in sync with each screen's backdrop.
/// </summary>
public static class StatusBarTheme
{
    private static readonly BindableProperty AppliedChromeProperty =
        BindableProperty.CreateAttached("AppliedChrome", typeof(ScreenChrome?), typeof(StatusBarTheme), null);

    public static Color PitchTop => Resolve(ScreenChrome.Pitch).Top;
    public static Color SlateTop => Resolve(ScreenChrome.Slate).Top;
    public static Color BrandTop => Resolve(ScreenChrome.Brand).Top;

    public static (Color Top, Color Mid, Color Bottom, bool DarkContent) Resolve(ScreenChrome chrome)
    {
        var light = false;
        try { light = ServiceHelper.GetService<IAppPreferences>().IsLight; }
        catch { /* DI not ready */ }

        if (light)
        {
            return chrome switch
            {
                ScreenChrome.Pitch => (
                    Color.FromArgb("#E7F5EC"),
                    Color.FromArgb("#C8E6C9"),
                    Color.FromArgb("#A5D6A7"),
                    DarkContent: true),
                ScreenChrome.Brand => (
                    Color.FromArgb("#E8F5E9"),
                    Color.FromArgb("#C8E6C9"),
                    Color.FromArgb("#A5D6A7"),
                    DarkContent: true),
                _ => (
                    Color.FromArgb("#F2F4F7"),
                    Color.FromArgb("#E4E9F0"),
                    Color.FromArgb("#D5DCE6"),
                    DarkContent: true),
            };
        }

        return chrome switch
        {
            ScreenChrome.Pitch => (
                Color.FromArgb("#06351A"),
                Color.FromArgb("#0B6B2E"),
                Color.FromArgb("#1FA85A"),
                DarkContent: false),
            ScreenChrome.Brand => (
                Color.FromArgb("#0B6B2E"),
                Color.FromArgb("#0B6B2E"),
                Color.FromArgb("#1FA85A"),
                DarkContent: false),
            _ => (
                Color.FromArgb("#0E1525"),
                Color.FromArgb("#1A2740"),
                Color.FromArgb("#2A3F5F"),
                DarkContent: false),
        };
    }

    public static void Apply(Page page, ScreenChrome chrome)
    {
        page.SetValue(AppliedChromeProperty, chrome);
        var (top, mid, bottom, darkContent) = Resolve(chrome);
        Apply(page, top, darkContent);
        TryPaintBackdrop(page, top, mid, bottom);
    }

    /// <summary>Re-applies chrome for the current Shell page after light/dark toggles.</summary>
    public static void RefreshCurrentPage()
    {
        try
        {
            if (Shell.Current?.CurrentPage is not Page page) return;
            if (page.GetValue(AppliedChromeProperty) is ScreenChrome chrome)
                Apply(page, chrome);
        }
        catch
        {
            // Ignore — theme toggle must stay resilient.
        }
    }

    /// <param name="darkContent">True for dark status-bar icons on a light background.</param>
    public static void Apply(Page page, Color background, bool darkContent = false)
    {
        if (page is null) return;

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                page.BackgroundColor = background;
#if ANDROID
                ApplyAndroid(background, darkContent);
#elif IOS
                ApplyIos(darkContent);
#endif
            }
            catch
            {
                // Platform status-bar updates can fail on mid-transition pages.
            }
        });
    }

    private static void TryPaintBackdrop(Page page, Color top, Color mid, Color bottom)
    {
        if (page is not ContentPage { Content: { } content }) return;
        var box = FindFirstBoxView(content);
        if (box is null) return;

        box.Background = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            [
                new GradientStop(top, 0),
                new GradientStop(mid, 0.5f),
                new GradientStop(bottom, 1),
            ],
        };
    }

    private static BoxView? FindFirstBoxView(IView view)
    {
        if (view is BoxView box) return box;
        if (view is not IVisualTreeElement tree) return null;
        foreach (var child in tree.GetVisualChildren())
        {
            if (child is IView childView)
            {
                var found = FindFirstBoxView(childView);
                if (found is not null) return found;
            }
        }
        return null;
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
        UIKit.UIApplication.SharedApplication.SetStatusBarStyle(
            darkContent ? UIKit.UIStatusBarStyle.DarkContent : UIKit.UIStatusBarStyle.LightContent,
            animated: true);
    }
#endif
}
