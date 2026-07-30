namespace CityLeague.App.Services;

public enum GlassDialogAccent
{
    Neutral,
    Accent,
    Destructive,
}

public sealed record GlassDialogChoice(string Id, string Label, GlassDialogAccent Accent = GlassDialogAccent.Neutral);

public interface IGlassDialogService
{
    Task AlertAsync(string title, string message, string ok = "OK");

    Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept = "OK",
        string cancel = "Cancel",
        bool destructive = false);

    /// <summary>Returns the selected choice id, or null if cancelled.</summary>
    Task<string?> ChooseAsync(string title, string? message, IReadOnlyList<GlassDialogChoice> choices, string cancel = "Cancel");

    /// <summary>Returns entered text, or null if cancelled.</summary>
    Task<string?> PromptAsync(
        string title,
        string? message,
        string accept = "Save",
        string cancel = "Cancel",
        string? initialValue = null,
        int maxLength = 80);
}
