using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using CityLeague.Core.Abstractions;
using CityLeague.Core.Dtos;
using Microsoft.Extensions.Options;

namespace CityLeague.Infrastructure.Storage;

/// <summary>Azure Blob Storage implementation of avatar storage with SAS upload tickets.</summary>
public class AzureBlobAvatarStorage : IAvatarStorage
{
    private readonly AvatarStorageOptions _options;
    private readonly BlobContainerClient _container;

    public AzureBlobAvatarStorage(IOptions<AvatarStorageOptions> options)
    {
        _options = options.Value;
        var service = new BlobServiceClient(_options.ConnectionString);
        _container = service.GetBlobContainerClient(_options.ContainerName);
    }

    public string BuildAvatarBlobPath(Guid userId, string fileExtension)
    {
        var ext = string.IsNullOrWhiteSpace(fileExtension) ? ".png" : fileExtension;
        if (!ext.StartsWith('.')) ext = "." + ext;
        return $"{userId:N}/{Guid.NewGuid():N}{ext}";
    }

    public async Task<string> SaveAsync(string blobPath, Stream content, string contentType, CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
        var blob = _container.GetBlobClient(blobPath);
        await blob.UploadAsync(content, new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType },
        }, ct);
        return blobPath;
    }

    public async Task<AvatarUploadTicket> CreateUploadTicketAsync(string blobPath, string contentType, CancellationToken ct = default)
    {
        await _container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);
        var blob = _container.GetBlobClient(blobPath);

        if (!blob.CanGenerateSasUri)
            throw new InvalidOperationException("Blob client cannot generate SAS. Use a connection string with an account key.");

        var sas = new BlobSasBuilder
        {
            BlobContainerName = _container.Name,
            BlobName = blobPath,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow.AddMinutes(-2),
            ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(_options.UploadSasMinutes),
        };
        sas.SetPermissions(BlobSasPermissions.Write | BlobSasPermissions.Create);

        var uploadUri = blob.GenerateSasUri(sas);
        return new AvatarUploadTicket(uploadUri.ToString(), blobPath, blob.Uri.ToString());
    }

    public string? ResolvePublicUrl(string? blobPath)
    {
        if (string.IsNullOrWhiteSpace(blobPath)) return null;
        return _container.GetBlobClient(blobPath).Uri.ToString();
    }
}
