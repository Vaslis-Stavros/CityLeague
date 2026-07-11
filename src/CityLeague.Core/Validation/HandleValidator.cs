using System.Text.RegularExpressions;

namespace CityLeague.Core.Validation;

/// <summary>Shared rules for unique handles: 3-20 chars, [a-z0-9_], not reserved.</summary>
public static partial class HandleValidator
{
    public const int MinLength = 3;
    public const int MaxLength = 20;

    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "administrator", "system", "root", "support", "help",
        "CityLeague", "count_me_in", "moderator", "owner", "null", "undefined",
        "me", "you", "everyone", "team", "official",
    };

    [GeneratedRegex("^[a-z0-9_]+$")]
    private static partial Regex HandleRegex();

    /// <summary>Normalizes a handle to its canonical (lower-case, trimmed) form.</summary>
    public static string Normalize(string handle) => (handle ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Validates a handle. Returns true when valid; otherwise sets <paramref name="reason"/>.</summary>
    public static bool IsValid(string? handle, out string? reason)
    {
        var normalized = Normalize(handle ?? string.Empty);

        if (normalized.Length < MinLength)
        {
            reason = $"Handle must be at least {MinLength} characters.";
            return false;
        }

        if (normalized.Length > MaxLength)
        {
            reason = $"Handle must be at most {MaxLength} characters.";
            return false;
        }

        if (!HandleRegex().IsMatch(normalized))
        {
            reason = "Handle may only contain lowercase letters, numbers and underscores.";
            return false;
        }

        if (Reserved.Contains(normalized))
        {
            reason = "That handle is reserved.";
            return false;
        }

        reason = null;
        return true;
    }
}
