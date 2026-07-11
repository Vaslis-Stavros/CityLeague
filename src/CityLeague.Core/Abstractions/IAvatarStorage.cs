using CityLeague.Core.Dtos;

namespace CityLeague.Core.Abstractions;

/// <summary>Abstracts avatar image storage (Azure Blob in production, local disk in dev).</summary>
public interface IAvatarStorage
{
    /// <summary>Builds a deterministic blob path for a user's avatar.</summary>
    string BuildAvatarBlobPath(Guid userId, string fileExtension);

    /// <summary>Uploads avatar bytes and returns the stored blob path.</summary>
    Task<string> SaveAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>Creates a direct-upload ticket (SAS for Azure). May be unsupported for some providers.</summary>
    Task<AvatarUploadTicket> CreateUploadTicketAsync(string blobPath, string contentType, CancellationToken ct = default);

    /// <summary>Resolves a stored blob path to a publicly reachable URL (absolute or app-relative).</summary>
    string? ResolvePublicUrl(string? blobPath);
}
