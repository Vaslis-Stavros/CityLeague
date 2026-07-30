using CityLeague.App.Services;

namespace CityLeague.App.Helpers;

/// <summary>Convenience facade so view-models can show glass dialogs without injecting the service everywhere.</summary>
public static class GlassDialog
{
    private static IGlassDialogService Service => ServiceHelper.GetService<IGlassDialogService>();

    public static Task AlertAsync(string title, string message, string ok = "OK")
        => Service.AlertAsync(title, message, ok);

    public static Task<bool> ConfirmAsync(
        string title,
        string message,
        string accept = "OK",
        string cancel = "Cancel",
        bool destructive = false)
        => Service.ConfirmAsync(title, message, accept, cancel, destructive);

    public static Task<string?> ChooseAsync(
        string title,
        string? message,
        IReadOnlyList<GlassDialogChoice> choices,
        string cancel = "Cancel")
        => Service.ChooseAsync(title, message, choices, cancel);

    public static Task<string?> ChooseAsync(string title, string? message, params string[] options)
        => Service.ChooseAsync(
            title,
            message,
            options.Select(o => new GlassDialogChoice(o, o)).ToList());

    public static Task<string?> PromptAsync(
        string title,
        string? message,
        string accept = "Save",
        string cancel = "Cancel",
        string? initialValue = null,
        int maxLength = 80)
        => Service.PromptAsync(title, message, accept, cancel, initialValue, maxLength);
}
