namespace CityLeague.Core.Validation;

/// <summary>Chooses a human display name from SSO claims / email / handle.</summary>
public static class DisplayNameResolver
{
    /// <summary>
    /// Prefer an explicit name, then the email local-part (the bit before @), then the handle.
    /// Never returns provider placeholders like "Google player".
    /// </summary>
    public static string Resolve(string? displayName, string? email, string? handle = null)
    {
        if (!string.IsNullOrWhiteSpace(displayName) && !IsPlaceholder(displayName))
            return displayName.Trim();

        var fromEmail = FromEmail(email);
        if (!string.IsNullOrWhiteSpace(fromEmail))
            return fromEmail!;

        if (!string.IsNullOrWhiteSpace(handle))
            return handle.Trim().TrimStart('@');

        return "Player";
    }

    /// <summary>alex@gmail.com → Alex</summary>
    public static string? FromEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            return null;

        var local = email.Split('@')[0].Trim();
        if (string.IsNullOrWhiteSpace(local))
            return null;

        // Turn dots/underscores into spaces for a friendlier first look.
        var cleaned = local.Replace('.', ' ').Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(cleaned))
            return local;

        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts.Select(Capitalize));
    }

    public static bool IsPlaceholder(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;

        var trimmed = name.Trim();
        if (string.Equals(trimmed, "Player", StringComparison.OrdinalIgnoreCase))
            return true;

        // "Google player", "Microsoft player", "Apple player", "Dev player", …
        return trimmed.EndsWith(" player", StringComparison.OrdinalIgnoreCase);
    }

    private static string Capitalize(string value)
    {
        if (value.Length == 0) return value;
        if (value.Length == 1) return value.ToUpperInvariant();
        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }
}
