namespace CityLeague.Core.Entities;

/// <summary>An application user, provisioned on first login from the identity provider.</summary>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Subject of the identity that created the account, prefixed by provider
    /// (e.g. "google:1039...", "local:alex_k"). Additional providers are recorded in
    /// <see cref="ExternalLogins"/>. Null only in seed/test data.
    /// </summary>
    public string? B2CObjectId { get; set; }

    public string? Email { get; set; }

    /// <summary>PBKDF2 hash for local email/password accounts. Null for external-provider users.</summary>
    public string? PasswordHash { get; set; }

    /// <summary>Globally unique, case-insensitive handle (e.g. "alex_k"). Null until onboarding completes.</summary>
    public string? UniqueHandle { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Blob path/URL of the user's avatar image. Null means fall back to initials.</summary>
    public string? AvatarBlobUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<Contact> ContactsOwned { get; set; } = new List<Contact>();
    public ICollection<PlayerSportStats> Stats { get; set; } = new List<PlayerSportStats>();
    public ICollection<UserExternalLogin> ExternalLogins { get; set; } = new List<UserExternalLogin>();
}
