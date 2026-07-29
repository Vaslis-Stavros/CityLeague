using CityLeague.Core.Abstractions;
using CityLeague.Core.Dtos;
using Microsoft.Extensions.Options;

namespace CityLeague.Infrastructure.Storage;

/// <summary>
/// Dev avatar storage that writes files under a local folder served as static content.
/// Direct SAS-style uploads are not supported; clients upload via the API's multipart endpoint.
/// </summary>
public class LocalAvatarStorage : IAvatarStorage
{
    private readonly AvatarStorageOptions _options;
    private readonly string _root;

    public LocalAvatarStorage(IOptions<AvatarStorageOptions> options)
    {
        _options = options.Value;
        _root = _options.LocalRootPath
            ?? Path.Combine(AppContext.BaseDirectory, "wwwroot", "uploads");
        Directory.CreateDirectory(_root);
    }

    public string BuildAvatarBlobPath(Guid userId, string fileExtension)
    {
        var ext = string.IsNullOrWhiteSpace(fileExtension) ? ".png" : fileExtension;
        if (!ext.StartsWith('.')) ext = "." + ext;
        return $"avatars/{userId:N}/{Guid.NewGuid():N}{ext}";
    }

    public async Task<string> SaveAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default)
    {
        var full = Path.Combine(_root, blobPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        await using var file = File.Create(full);
        await content.CopyToAsync(file, ct);
        return blobPath;
    }

    public Task<AvatarUploadTicket> CreateUploadTicketAsync(string blobPath, string contentType, CancellationToken ct = default)
        => throw new NotSupportedException("Local storage does not support direct upload tickets; use the API multipart upload endpoint.");

    public string? ResolvePublicUrl(string? blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath)) return null;

        if (blobPath.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || blobPath.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            return blobPath;

        var relative = blobPath.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
            ? blobPath
            : $"/uploads/{blobPath.TrimStart('/')}";

        // Optional override for reverse proxies / CDNs. When empty, callers (ApiMapper) should
        // absolutize against the incoming request host so mobile clients get a reachable URL.
        return string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
            ? relative
            : $"{_options.PublicBaseUrl.TrimEnd('/')}{relative}";
    }
}
