namespace CityLeague.Core.Dtos;

public record UserDto(
    Guid Id,
    string? Handle,
    string DisplayName,
    string? Email,
    string? AvatarUrl);

public record SetHandleRequest(string Handle);

public record HandleAvailabilityDto(string Handle, bool Available, string? Reason);

public record UpdateProfileRequest(string? DisplayName, string? AvatarBlobPath);

/// <summary>A pre-signed ticket the client uses to upload an avatar directly to blob storage.</summary>
public record AvatarUploadTicket(string UploadUrl, string BlobPath, string PublicUrl);

public record UserSearchResultDto(
    Guid Id,
    string Handle,
    string DisplayName,
    string? AvatarUrl,
    string Relationship);
