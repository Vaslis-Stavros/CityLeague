using CityLeague.App.Helpers;
using CityLeague.App.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CityLeague.App.Controls;

/// <summary>Shows a user's avatar image, or a colored circle with initials as a fallback.</summary>
public class AvatarView : ContentView
{
    public static readonly BindableProperty ImageUrlProperty =
        BindableProperty.Create(nameof(ImageUrl), typeof(string), typeof(AvatarView), null, propertyChanged: OnChanged);

    public static readonly BindableProperty DisplayNameProperty =
        BindableProperty.Create(nameof(DisplayName), typeof(string), typeof(AvatarView), null, propertyChanged: OnChanged);

    public static readonly BindableProperty DiameterProperty =
        BindableProperty.Create(nameof(Diameter), typeof(double), typeof(AvatarView), 44.0, propertyChanged: OnChanged);

    private readonly Border _border;
    private readonly Label _initials;
    private readonly Image _image;
    private string? _loadedUrl;

    public AvatarView()
    {
        _initials = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            InputTransparent = true,
        };

        _image = new Image
        {
            Aspect = Aspect.AspectFill,
            IsVisible = false,
            InputTransparent = true,
        };

        _border = new Border
        {
            StrokeThickness = 0,
            Padding = 0,
            // Clip children to the rounded shape (otherwise the photo shows as a square).
            Content = new Grid { Children = { _initials, _image }, IsClippedToBounds = true },
        };

        Content = _border;
        Render();
    }

    public string? ImageUrl { get => (string?)GetValue(ImageUrlProperty); set => SetValue(ImageUrlProperty, value); }
    public string? DisplayName { get => (string?)GetValue(DisplayNameProperty); set => SetValue(DisplayNameProperty, value); }
    public double Diameter { get => (double)GetValue(DiameterProperty); set => SetValue(DiameterProperty, value); }

    private static void OnChanged(BindableObject bindable, object oldValue, object newValue)
        => ((AvatarView)bindable).Render();

    private void Render()
    {
        var d = Math.Max(Diameter, 1);
        _border.WidthRequest = d;
        _border.HeightRequest = d;
        _border.StrokeShape = new RoundRectangle { CornerRadius = d / 2 };
        _border.BackgroundColor = AvatarFormatter.ColorFor(DisplayName);
        _border.Clip = new EllipseGeometry
        {
            Center = new Point(d / 2, d / 2),
            RadiusX = d / 2,
            RadiusY = d / 2,
        };

        _initials.Text = AvatarFormatter.Initials(DisplayName);
        _initials.FontSize = d * 0.4;
        // Always keep initials underneath: if the remote image fails to load the circle
        // still shows something instead of going blank.
        _initials.IsVisible = true;

        var absolute = Absolutize(ImageUrl);
        if (string.IsNullOrWhiteSpace(absolute))
        {
            _image.Source = null;
            _image.IsVisible = false;
            _loadedUrl = null;
            return;
        }

        if (string.Equals(_loadedUrl, absolute, StringComparison.Ordinal))
        {
            _image.IsVisible = true;
            return;
        }

        _loadedUrl = absolute;
        _image.Source = new UriImageSource
        {
            Uri = new Uri(absolute, UriKind.Absolute),
            CachingEnabled = true,
            CacheValidity = TimeSpan.FromDays(1),
        };
        _image.IsVisible = true;
    }

    /// <summary>
    /// The API may return a relative /uploads/... path (or an absolute URL pointed at the
    /// wrong host). Always fetch through the same base URL the rest of the app uses.
    /// </summary>
    private static string? Absolutize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;

        url = url.Trim();

        string baseUrl;
        try
        {
            baseUrl = ServiceHelper.GetService<ApiSettings>().BaseUrl.TrimEnd('/');
        }
        catch
        {
            return Uri.TryCreate(url, UriKind.Absolute, out _) ? url : null;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            // Rewrite loopback hosts so the Android emulator / device can reach the API
            // machine. The API itself may have stamped localhost into the URL.
            if (IsLoopback(absolute.Host) && Uri.TryCreate(baseUrl, UriKind.Absolute, out var apiBase))
                return new Uri(apiBase, absolute.PathAndQuery).ToString();

            return absolute.ToString();
        }

        if (url.StartsWith('/'))
            return $"{baseUrl}{url}";

        return $"{baseUrl}/{url}";
    }

    private static bool IsLoopback(string host) =>
        string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase)
        || host is "127.0.0.1" or "::1";
}
