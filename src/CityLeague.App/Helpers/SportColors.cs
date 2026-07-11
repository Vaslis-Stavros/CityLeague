namespace CityLeague.App.Helpers;

/// <summary>Per-sport accent colors for chips and UI highlights.</summary>
public static class SportColors
{
    public static Color GetColor(string? sportKey) => (sportKey ?? string.Empty).ToLowerInvariant() switch
    {
        "football" => Color.FromArgb("#0B6B2E"),
        "padel" => Color.FromArgb("#1565C0"),
        "tennis" => Color.FromArgb("#F9A825"),
        "basketball" => Color.FromArgb("#E65100"),
        "other" => Color.FromArgb("#607D8B"),
        _ => Color.FromArgb("#607D8B"),
    };

    public static Color GetLightColor(string? sportKey)
    {
        var baseColor = GetColor(sportKey);
        return baseColor.WithAlpha(0.18f);
    }
}
