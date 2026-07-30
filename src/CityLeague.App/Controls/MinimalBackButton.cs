namespace CityLeague.App.Controls;

/// <summary>Minimal top-left back chevron for secondary (non-tab) screens.</summary>
public class MinimalBackButton : ContentView
{
    public MinimalBackButton()
    {
        HorizontalOptions = LayoutOptions.Start;
        VerticalOptions = LayoutOptions.Start;
        Padding = new Thickness(0, 0, 8, 0);
        Margin = new Thickness(-4, -6, 0, 2);

        Content = new Label
        {
            Text = "‹",
            FontFamily = "OutfitSemiBold",
            FontSize = 34,
            TextColor = Colors.White,
            Opacity = 0.92,
            VerticalOptions = LayoutOptions.Center,
            HorizontalOptions = LayoutOptions.Start,
            Margin = new Thickness(0, -10, 0, -6),
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            try
            {
                await Shell.Current.GoToAsync("..");
            }
            catch
            {
                // Already at root / navigation stack empty.
            }
        };
        GestureRecognizers.Add(tap);
    }
}
