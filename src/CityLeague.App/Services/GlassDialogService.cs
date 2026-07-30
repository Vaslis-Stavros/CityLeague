using CityLeague.App.Views;

namespace CityLeague.App.Services;

public sealed class GlassDialogService : IGlassDialogService
{
    public async Task AlertAsync(string title, string message, string ok = "OK")
    {
        await ShowAsync(new GlassDialogRequest(
            title,
            message,
            [new GlassDialogChoice("ok", ok, GlassDialogAccent.Accent)],
            CancelLabel: null));
    }

    public async Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept = "OK",
        string cancel = "Cancel",
        bool destructive = false)
    {
        var result = await ShowAsync(new GlassDialogRequest(
            title,
            message,
            [
                new GlassDialogChoice("accept", accept,
                    destructive ? GlassDialogAccent.Destructive : GlassDialogAccent.Accent),
            ],
            cancel));
        return result == "accept";
    }

    public Task<string?> ChooseAsync(
        string title,
        string? message,
        IReadOnlyList<GlassDialogChoice> choices,
        string cancel = "Cancel")
        => ShowAsync(new GlassDialogRequest(title, message, choices, cancel));

    public Task<string?> PromptAsync(
        string title,
        string? message,
        string accept = "Save",
        string cancel = "Cancel",
        string? initialValue = null,
        int maxLength = 80)
        => ShowAsync(new GlassDialogRequest(
            title,
            message,
            [new GlassDialogChoice("accept", accept, GlassDialogAccent.Accent)],
            cancel,
            Prompt: true,
            InitialValue: initialValue,
            MaxLength: maxLength));

    private static async Task<string?> ShowAsync(GlassDialogRequest request)
    {
        var shell = Shell.Current;
        if (shell is null)
            return null;

        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var page = new GlassDialogPage(request, result => tcs.TrySetResult(result));

        await MainThread.InvokeOnMainThreadAsync(async () =>
            await shell.Navigation.PushModalAsync(page, animated: false));

        return await tcs.Task;
    }
}

public sealed record GlassDialogRequest(
    string Title,
    string? Message,
    IReadOnlyList<GlassDialogChoice> Choices,
    string? CancelLabel,
    bool Prompt = false,
    string? InitialValue = null,
    int MaxLength = 80);
