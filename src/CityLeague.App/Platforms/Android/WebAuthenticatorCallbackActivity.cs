using Android.App;
using Android.Content;
using Android.Content.PM;

namespace CityLeague.App;

/// <summary>
/// Receives the OAuth redirect for <see cref="CallbackScheme"/>. Must stay in sync with the
/// API's Auth:MobileRedirectUri setting.
/// </summary>
[Activity(NoHistory = true, LaunchMode = LaunchMode.SingleTop, Exported = true)]
[IntentFilter(
    [Intent.ActionView],
    Categories = [Intent.CategoryDefault, Intent.CategoryBrowsable],
    DataScheme = CallbackScheme,
    DataHost = CallbackHost)]
public class WebAuthenticatorCallbackActivity : Microsoft.Maui.Authentication.WebAuthenticatorCallbackActivity
{
    public const string CallbackScheme = "cityleague";
    public const string CallbackHost = "auth";
}
