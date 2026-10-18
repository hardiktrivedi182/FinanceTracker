using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FinanceTracker.Application.Statements.Storage;
using Microsoft.Extensions.Options;

namespace FinanceTracker.Infrastructure.Storage;

public class AzureBlobStorageOptions
{
    public const string SectionName = "StatementStorage:AzureBlob";
    public string? ServiceUri { get; set; }
    public string? ConnectionString { get; set; }
    public string ContainerName { get; set; } = "statements";
}

/// <summary>
/// Azure Blob Storage implementation of IStatementStorage.
/// Uses private containers (PublicAccessType.None) and Managed Identity in Azure environments.
/// </summary>
public class AzureBlobStatementStorage : IStatementStorage
{
    private readonly BlobContainerClient _containerClient;

    public AzureBlobStatementStorage(IOptions<AzureBlobStorageOptions> options)
    {
        var opts = options.Value;

        if (string.IsNullOrWhiteSpace(opts.ContainerName))
        {
            throw new InvalidOperationException("Azure Blob container name must be configured.");
        }

        BlobServiceClient serviceClient;

        if (!string.IsNullOrWhiteSpace(opts.ConnectionString))
        {
            // For testing against Azurite local emulator or explicit connection string
            serviceClient = new BlobServiceClient(opts.ConnectionString);
        }
        else if (!string.IsNullOrWhiteSpace(opts.ServiceUri) && Uri.TryCreate(opts.ServiceUri, UriKind.Absolute, out var uri))
        {
            // Production standard: Managed Identity via DefaultAzureCredential
            serviceClient = new BlobServiceClient(uri, new DefaultAzureCredential());
        }
        else
        {
            throw new InvalidOperationException(
                "Azure Blob Storage is configured but neither ServiceUri nor ConnectionString was provided.");
        }

        _containerClient = serviceClient.GetBlobContainerClient(opts.ContainerName);
    }

    public async Task<string> SaveAsync(Guid householdId, Guid statementId, Stream content, string extension, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var safeExt = extension.StartsWith('.') ? extension : "." + extension;
        if (safeExt.Contains('/') || safeExt.Contains('\\'))
        {
            safeExt = ".csv";
        }

        var blobName = $"{householdId}/{statementId}{safeExt}";
        var blobClient = _containerClient.GetBlobClient(blobName);

        // Ensure private container exists without public access
        await _containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);

        if (content.CanSeek)
        {
            content.Seek(0, SeekOrigin.Begin);
        }

        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = "text/csv"
            }
        };

        await blobClient.UploadAsync(content, uploadOptions, ct);

        return blobName;
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        var blobClient = _containerClient.GetBlobClient(storageKey);
        var response = await blobClient.OpenReadAsync(cancellationToken: ct);
        return response;
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var blobClient = _containerClient.GetBlobClient(storageKey);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
    }

    public async Task<bool> ExistsAsync(string storageKey, CancellationToken ct = default)
    {
        var blobClient = _containerClient.GetBlobClient(storageKey);
        var exists = await blobClient.ExistsAsync(ct);
        return exists.Value;
    }
}
