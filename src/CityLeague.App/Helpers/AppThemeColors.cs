namespace CityLeague.App.Helpers;

/// <summary>
/// Applies light/dark glass palettes by overwriting DynamicResource keys in place.
/// Avoids removing/re-adding ResourceDictionaries, which crashes MAUI on theme toggle.
/// </summary>
public static class AppThemeColors
{
    public static readonly IReadOnlyDictionary<string, Color> Dark = new Dictionary<string, Color>
    {
        ["PageTitle"] = Color.FromArgb("#FFFFFF"),
        ["PageMuted"] = Color.FromArgb("#C8E6C8"),
        ["PageSoft"] = Color.FromArgb("#A8D5B5"),
        ["PageFaint"] = Color.FromArgb("#EAF7EE"),
        ["PageBody"] = Color.FromArgb("#E6F4EA"),
        ["PageMeta"] = Color.FromArgb("#D5EBD8"),
        ["SlateTitle"] = Color.FromArgb("#FFFFFF"),
        ["SlateMuted"] = Color.FromArgb("#A8B8CC"),
        ["SlateSoft"] = Color.FromArgb("#8FA0B5"),
        ["ThemeGlassFill"] = Color.FromArgb("#38FFFFFF"),
        ["ThemeGlassStroke"] = Color.FromArgb("#55FFFFFF"),
        ["ThemeGlassFillStrong"] = Color.FromArgb("#55FFFFFF"),
        ["ThemeChipFill"] = Color.FromArgb("#28FFFFFF"),
        ["ThemeChipStroke"] = Color.FromArgb("#55FFFFFF"),
        ["ThemeProgressTrack"] = Color.FromArgb("#33FFFFFF"),
        ["ThemeDateChipFill"] = Color.FromArgb("#28FFFFFF"),
    };

    public static readonly IReadOnlyDictionary<string, Color> Light = new Dictionary<string, Color>
    {
        ["PageTitle"] = Color.FromArgb("#14261A"),
        ["PageMuted"] = Color.FromArgb("#3D6B4E"),
        ["PageSoft"] = Color.FromArgb("#4A7A5C"),
        ["PageFaint"] = Color.FromArgb("#14261A"),
        ["PageBody"] = Color.FromArgb("#3D6B4E"),
        ["PageMeta"] = Color.FromArgb("#3D6B4E"),
        ["SlateTitle"] = Color.FromArgb("#152033"),
        ["SlateMuted"] = Color.FromArgb("#4A5C70"),
        ["SlateSoft"] = Color.FromArgb("#4A5C70"),
        ["ThemeGlassFill"] = Color.FromArgb("#1A000000"),
        ["ThemeGlassStroke"] = Color.FromArgb("#28000000"),
        ["ThemeGlassFillStrong"] = Color.FromArgb("#24000000"),
        ["ThemeChipFill"] = Color.FromArgb("#22000000"),
        ["ThemeChipStroke"] = Color.FromArgb("#33000000"),
        ["ThemeProgressTrack"] = Color.FromArgb("#22000000"),
        ["ThemeDateChipFill"] = Color.FromArgb("#22000000"),
    };

    public static void Apply(bool light)
    {
        if (Application.Current?.Resources is not { } resources)
            return;

        var map = light ? Light : Dark;
        foreach (var (key, color) in map)
            resources[key] = color;
    }
}
