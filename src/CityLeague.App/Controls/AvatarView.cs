using CityLeague.App.Helpers;

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

    public AvatarView()
    {
        _initials = new Label
        {
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
        };

        _image = new Image { Aspect = Aspect.AspectFill, IsVisible = false };

        _border = new Border
        {
            StrokeThickness = 0,
            Content = new Grid { Children = { _initials, _image } },
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
        var d = Diameter;
        _border.WidthRequest = d;
        _border.HeightRequest = d;
        _border.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = d / 2 };
        _border.BackgroundColor = AvatarFormatter.ColorFor(DisplayName);

        _initials.Text = AvatarFormatter.Initials(DisplayName);
        _initials.FontSize = d * 0.4;

        if (!string.IsNullOrWhiteSpace(ImageUrl))
        {
            _image.Source = ImageUrl;
            _image.IsVisible = true;
            _initials.IsVisible = false;
        }
        else
        {
            _image.IsVisible = false;
            _initials.IsVisible = true;
        }
    }
}
