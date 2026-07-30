using Android.App;
using Android.Content.PM;
using Android.OS;

namespace CityLeague.App;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Default to the slate glass top until a page applies its own theme.
        // Per-screen colors are set from StatusBarTheme.Apply in OnAppearing.
        if (Build.VERSION.SdkInt < BuildVersionCodes.Lollipop || Window is null)
            return;

        Window.AddFlags(Android.Views.WindowManagerFlags.DrawsSystemBarBackgrounds);
        Window.ClearFlags(Android.Views.WindowManagerFlags.TranslucentStatus);
#pragma warning disable CA1422
        Window.SetStatusBarColor(Android.Graphics.Color.ParseColor("#0E1525"));
#pragma warning restore CA1422

        if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
        {
            Window.InsetsController?.SetSystemBarsAppearance(
                0,
                (int)Android.Views.WindowInsetsControllerAppearance.LightStatusBars);
        }
        else if (Build.VERSION.SdkInt >= BuildVersionCodes.M && Window.DecorView is { } decor)
        {
#pragma warning disable CA1422
            decor.SystemUiVisibility = 0;
#pragma warning restore CA1422
        }
    }
}
