namespace CityLeague.Core.Entities;

/// <summary>
/// A verified identity-provider account bound to a <see cref="User"/>. One user can have
/// several logins (Google, Microsoft, Apple) so signing in with a second provider on the
/// same verified email lands on the existing account instead of creating a duplicate.
/// </summary>
public class UserExternalLogin
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Normalized provider key: "google", "microsoft", "apple", "b2c" or "dev".</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>Immutable subject identifier issued by the provider (the "sub"/"oid" claim).</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Email as asserted by the provider at the last sign-in. Informational only.</summary>
    public string? Email { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset LastLoginAt { get; set; } = DateTimeOffset.UtcNow;
}
