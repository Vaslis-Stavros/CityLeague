using CityLeague.App.Services;
using Microsoft.Maui.Controls.Shapes;

namespace CityLeague.App.Views;

public partial class GlassDialogPage : ContentPage
{
    private readonly GlassDialogRequest _request;
    private readonly Action<string?> _onClosed;
    private bool _closing;

    public GlassDialogPage(GlassDialogRequest request, Action<string?> onClosed)
    {
        InitializeComponent();
        _request = request;
        _onClosed = onClosed;

        TitleLabel.Text = request.Title;
        MessageLabel.Text = request.Message ?? string.Empty;
        MessageLabel.IsVisible = !string.IsNullOrWhiteSpace(request.Message);

        CancelButton.IsVisible = !string.IsNullOrWhiteSpace(request.CancelLabel);
        CancelLabel.Text = request.CancelLabel ?? "Cancel";

        PromptHost.IsVisible = request.Prompt;
        if (request.Prompt)
        {
            PromptEntry.Text = request.InitialValue ?? string.Empty;
            PromptEntry.MaxLength = Math.Max(1, request.MaxLength);
            PromptEntry.Completed += async (_, _) => await AcceptPromptAsync();
        }

        foreach (var choice in request.Choices)
            ChoicesStack.Children.Add(BuildChoiceButton(choice));
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Card.Opacity = 0;
        Card.Scale = 0.94;
        await Task.WhenAll(
            Card.FadeTo(1, 180, Easing.CubicOut),
            Card.ScaleTo(1, 220, Easing.CubicOut));

        if (_request.Prompt)
            PromptEntry.Focus();
    }

    protected override bool OnBackButtonPressed()
    {
        _ = CloseAsync(null);
        return true;
    }

    private View BuildChoiceButton(GlassDialogChoice choice)
    {
        var border = new Border
        {
            StrokeThickness = choice.Accent == GlassDialogAccent.Destructive ? 1 : 0,
            Stroke = choice.Accent == GlassDialogAccent.Destructive
                ? Color.FromArgb("#88FF8A80")
                : Colors.Transparent,
            BackgroundColor = choice.Accent switch
            {
                GlassDialogAccent.Destructive => Color.FromArgb("#33C0392B"),
                GlassDialogAccent.Accent => Colors.White,
                _ => Color.FromArgb("#28FFFFFF"),
            },
            StrokeShape = new RoundRectangle { CornerRadius = 18 },
            Padding = new Thickness(14, 12),
        };

        var label = new Label
        {
            Text = choice.Label,
            FontFamily = "OutfitSemiBold",
            FontSize = 15,
            TextColor = choice.Accent switch
            {
                GlassDialogAccent.Destructive => Color.FromArgb("#FFCDD2"),
                GlassDialogAccent.Accent => Color.FromArgb("#0B6B2E"),
                _ => Colors.White,
            },
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        border.Content = label;
        border.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                if (_request.Prompt && choice.Id == "accept")
                    await AcceptPromptAsync();
                else
                    await CloseAsync(choice.Id);
            }),
        });
        return border;
    }

    private async Task AcceptPromptAsync()
    {
        var text = PromptEntry.Text?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return;
        await CloseAsync(text);
    }

    private async void OnCancelTapped(object? sender, TappedEventArgs e)
        => await CloseAsync(null);

    private async Task CloseAsync(string? result)
    {
        if (_closing) return;
        _closing = true;
        try
        {
            await Task.WhenAll(
                Card.FadeTo(0, 120, Easing.CubicIn),
                Card.ScaleTo(0.96, 120, Easing.CubicIn));
            await Navigation.PopModalAsync(animated: false);
        }
        catch
        {
            // best-effort dismiss
        }
        finally
        {
            _onClosed(result);
        }
    }
}
