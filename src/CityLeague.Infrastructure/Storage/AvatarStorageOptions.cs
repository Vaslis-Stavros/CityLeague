namespace CityLeague.Infrastructure.Storage;

public class AvatarStorageOptions
{
    public const string SectionName = "AvatarStorage";

    /// <summary>"Local" (disk, for dev) or "Azure" (Blob Storage).</summary>
    public string Provider { get; set; } = "Local";

    // Local provider
    /// <summary>Absolute path to the folder served as static files (defaults to wwwroot/uploads).</summary>
    public string? LocalRootPath { get; set; }

    /// <summary>Absolute base URL used to build public avatar URLs in dev, e.g. http://10.0.2.2:5080.</summary>
    public string? PublicBaseUrl { get; set; }

    // Azure provider
    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "avatars";

    /// <summary>Minutes a generated upload SAS remains valid.</summary>
    public int UploadSasMinutes { get; set; } = 15;
}
