using Microsoft.Maui.Graphics;

namespace CityLeague.App.Helpers;

/// <summary>Produces initials and a deterministic color for avatar fallbacks.</summary>
public static class AvatarFormatter
{
    private static readonly Color[] Palette =
    [
        Color.FromArgb("#2563EB"), Color.FromArgb("#16A34A"), Color.FromArgb("#DC2626"),
        Color.FromArgb("#9333EA"), Color.FromArgb("#EA580C"), Color.FromArgb("#0891B2"),
        Color.FromArgb("#DB2777"), Color.FromArgb("#65A30D"), Color.FromArgb("#4F46E5"),
        Color.FromArgb("#0D9488"),
    ];

    public static string Initials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Trim().Split([' ', '_', '-', '.'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
            return "?";
        if (parts.Length == 1)
            return parts[0].Length >= 2 ? parts[0][..2].ToUpperInvariant() : parts[0].ToUpperInvariant();

        return $"{char.ToUpperInvariant(parts[0][0])}{char.ToUpperInvariant(parts[^1][0])}";
    }

    public static Color ColorFor(string? seed)
    {
        if (string.IsNullOrEmpty(seed))
            return Palette[0];

        var hash = 0;
        foreach (var ch in seed)
            hash = unchecked(hash * 31 + ch);
        var index = (int)((uint)hash % (uint)Palette.Length);
        return Palette[index];
    }
}
