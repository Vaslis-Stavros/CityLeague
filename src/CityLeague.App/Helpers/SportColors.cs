namespace CityLeague.App.Helpers;

/// <summary>Per-sport accent colors and full-screen backdrop themes for the home glass UI.</summary>
public static class SportColors
{
    public readonly record struct BackdropTheme(
        Color Top,
        Color Mid,
        Color Bottom,
        Color Glow,
        Color SoftText,
        Color SoftMuted,
        Color Accent);

    public static Color GetColor(string? sportKey) => GetTheme(sportKey).Accent;

    public static Color GetLightColor(string? sportKey)
        => GetColor(sportKey).WithAlpha(0.18f);

    public static BackdropTheme GetTheme(string? sportKey) => (sportKey ?? string.Empty).ToLowerInvariant() switch
    {
        // Pitch green
        "football" => new(
            Top: Color.FromArgb("#06351A"),
            Mid: Color.FromArgb("#0B6B2E"),
            Bottom: Color.FromArgb("#1FA85A"),
            Glow: Color.FromArgb("#33F2A900"),
            SoftText: Color.FromArgb("#DDEFDD"),
            SoftMuted: Color.FromArgb("#C8E6C8"),
            Accent: Color.FromArgb("#0B6B2E")),

        // Blue padel court
        "padel" => new(
            Top: Color.FromArgb("#061A33"),
            Mid: Color.FromArgb("#0D47A1"),
            Bottom: Color.FromArgb("#1E88E5"),
            Glow: Color.FromArgb("#334FC3F7"),
            SoftText: Color.FromArgb("#D6EAF8"),
            SoftMuted: Color.FromArgb("#B3D4F0"),
            Accent: Color.FromArgb("#1565C0")),

        // Deep court green with tennis-ball glow
        "tennis" => new(
            Top: Color.FromArgb("#142008"),
            Mid: Color.FromArgb("#3D5C12"),
            Bottom: Color.FromArgb("#7CB342"),
            Glow: Color.FromArgb("#55F9A825"),
            SoftText: Color.FromArgb("#E8F5C8"),
            SoftMuted: Color.FromArgb("#D4E8A8"),
            Accent: Color.FromArgb("#F9A825")),

        // Hardwood orange
        "basketball" => new(
            Top: Color.FromArgb("#2A1005"),
            Mid: Color.FromArgb("#BF360C"),
            Bottom: Color.FromArgb("#FF6D00"),
            Glow: Color.FromArgb("#44FFB74D"),
            SoftText: Color.FromArgb("#FFE8D6"),
            SoftMuted: Color.FromArgb("#FFCCBC"),
            Accent: Color.FromArgb("#E65100")),

        // Neutral slate
        "other" => new(
            Top: Color.FromArgb("#12151A"),
            Mid: Color.FromArgb("#37474F"),
            Bottom: Color.FromArgb("#78909C"),
            Glow: Color.FromArgb("#33B0BEC5"),
            SoftText: Color.FromArgb("#E0E6EA"),
            SoftMuted: Color.FromArgb("#C5CED6"),
            Accent: Color.FromArgb("#607D8B")),

        _ => GetTheme("other"),
    };
}
