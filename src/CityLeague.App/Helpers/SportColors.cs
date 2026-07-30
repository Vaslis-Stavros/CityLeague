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

    public static BackdropTheme GetTheme(string? sportKey)
    {
        var light = false;
        try { light = ServiceHelper.GetService<Services.IAppPreferences>().IsLight; }
        catch { /* DI not ready */ }
        return GetTheme(sportKey, light);
    }

    public static BackdropTheme GetTheme(string? sportKey, bool light) => (sportKey ?? string.Empty).ToLowerInvariant() switch
    {
        "football" => light
            ? new(
                Top: Color.FromArgb("#E8F6EC"),
                Mid: Color.FromArgb("#C8E8D0"),
                Bottom: Color.FromArgb("#A5D6A7"),
                Glow: Color.FromArgb("#44F2A900"),
                SoftText: Color.FromArgb("#12261A"),
                SoftMuted: Color.FromArgb("#3D6B4E"),
                Accent: Color.FromArgb("#0B6B2E"))
            : new(
                Top: Color.FromArgb("#06351A"),
                Mid: Color.FromArgb("#0B6B2E"),
                Bottom: Color.FromArgb("#1FA85A"),
                Glow: Color.FromArgb("#33F2A900"),
                SoftText: Color.FromArgb("#DDEFDD"),
                SoftMuted: Color.FromArgb("#C8E6C8"),
                Accent: Color.FromArgb("#0B6B2E")),

        "padel" => light
            ? new(
                Top: Color.FromArgb("#E8F1FB"),
                Mid: Color.FromArgb("#C5DCF5"),
                Bottom: Color.FromArgb("#90CAF9"),
                Glow: Color.FromArgb("#444FC3F7"),
                SoftText: Color.FromArgb("#0D2137"),
                SoftMuted: Color.FromArgb("#3A5F8A"),
                Accent: Color.FromArgb("#1565C0"))
            : new(
                Top: Color.FromArgb("#061A33"),
                Mid: Color.FromArgb("#0D47A1"),
                Bottom: Color.FromArgb("#1E88E5"),
                Glow: Color.FromArgb("#334FC3F7"),
                SoftText: Color.FromArgb("#D6EAF8"),
                SoftMuted: Color.FromArgb("#B3D4F0"),
                Accent: Color.FromArgb("#1565C0")),

        "tennis" => light
            ? new(
                Top: Color.FromArgb("#F2F7E6"),
                Mid: Color.FromArgb("#DCE8B8"),
                Bottom: Color.FromArgb("#C5E1A5"),
                Glow: Color.FromArgb("#55F9A825"),
                SoftText: Color.FromArgb("#1A2A0C"),
                SoftMuted: Color.FromArgb("#4A6B28"),
                Accent: Color.FromArgb("#F9A825"))
            : new(
                Top: Color.FromArgb("#142008"),
                Mid: Color.FromArgb("#3D5C12"),
                Bottom: Color.FromArgb("#7CB342"),
                Glow: Color.FromArgb("#55F9A825"),
                SoftText: Color.FromArgb("#E8F5C8"),
                SoftMuted: Color.FromArgb("#D4E8A8"),
                Accent: Color.FromArgb("#F9A825")),

        "basketball" => light
            ? new(
                Top: Color.FromArgb("#FFF1E8"),
                Mid: Color.FromArgb("#FFD0B5"),
                Bottom: Color.FromArgb("#FFAB91"),
                Glow: Color.FromArgb("#44FFB74D"),
                SoftText: Color.FromArgb("#3A1608"),
                SoftMuted: Color.FromArgb("#8A4020"),
                Accent: Color.FromArgb("#E65100"))
            : new(
                Top: Color.FromArgb("#2A1005"),
                Mid: Color.FromArgb("#BF360C"),
                Bottom: Color.FromArgb("#FF6D00"),
                Glow: Color.FromArgb("#44FFB74D"),
                SoftText: Color.FromArgb("#FFE8D6"),
                SoftMuted: Color.FromArgb("#FFCCBC"),
                Accent: Color.FromArgb("#E65100")),

        "other" => light
            ? new(
                Top: Color.FromArgb("#F2F4F7"),
                Mid: Color.FromArgb("#DCE3EA"),
                Bottom: Color.FromArgb("#B0BEC5"),
                Glow: Color.FromArgb("#33B0BEC5"),
                SoftText: Color.FromArgb("#152033"),
                SoftMuted: Color.FromArgb("#4A5C70"),
                Accent: Color.FromArgb("#607D8B"))
            : new(
                Top: Color.FromArgb("#12151A"),
                Mid: Color.FromArgb("#37474F"),
                Bottom: Color.FromArgb("#78909C"),
                Glow: Color.FromArgb("#33B0BEC5"),
                SoftText: Color.FromArgb("#E0E6EA"),
                SoftMuted: Color.FromArgb("#C5CED6"),
                Accent: Color.FromArgb("#607D8B")),

        _ => GetTheme("other", light),
    };
}
