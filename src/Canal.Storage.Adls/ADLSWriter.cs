using Azure.Storage.Blobs;
using System.Text;

namespace Canal.Storage.Adls;

public enum BlobCategory { Content, MetaData, Watermark }

public static partial class ADLSWriter
{

    /// <summary>
    /// Writes BOTH the payload content blob and its sidecar metadata blob (metadata remains JSON).
    /// </summary>
    public static Task SavePayloadAndMetadata(BlobContainerClient container
                                            , string environmentName, bool dataSourceIsExternal, string ingestionDomain, string vendorName, string resourceName, int apiVersion
                                            , string ingestionRunId, string requestId, int pageNr, string? contentJson, string metaDataJson
                                            , CancellationToken cancellationToken = default)
    {
        var contentBytes = Encoding.UTF8.GetBytes(contentJson ?? string.Empty);
        return SavePayloadAndMetadata(container, environmentName, dataSourceIsExternal, ingestionDomain, vendorName, resourceName, apiVersion, ingestionRunId, requestId, pageNr, contentBytes, metaDataJson, cancellationToken);
    }

    /// <summary>
    /// Overload for when the payload is not JSON (or you already have bytes instead of strings).
    /// The sidecar metadata remains a JSON string.
    /// </summary>
    public static async Task SavePayloadAndMetadata(BlobContainerClient container
                                                  , string environmentName, bool dataSourceIsExternal, string ingestionDomain, string vendorName, string resourceName, int apiVersion
                                                  , string ingestionRunId, string requestId, int pageNr, byte[] contentBytes, string metaDataJson
                                                  , CancellationToken cancellationToken = default)
    {
        _ = container ?? throw new ArgumentNullException(nameof(container));
        _ = contentBytes ?? throw new ArgumentNullException(nameof(contentBytes));

        // Validate naming inputs (ADLSBlobNamer will re-validate too, but failing early keeps errors local).
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName, nameof(environmentName));
        ArgumentException.ThrowIfNullOrWhiteSpace(ingestionDomain, nameof(ingestionDomain));
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorName, nameof(vendorName));
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName, nameof(resourceName));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(apiVersion, nameof(apiVersion));

        ArgumentException.ThrowIfNullOrWhiteSpace(ingestionRunId, nameof(ingestionRunId));
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId, nameof(requestId));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNr, nameof(pageNr));

        string contentBlobName = ADLSBlobNamer.GetBlobName(BlobCategory.Content, environmentName, dataSourceIsExternal, ingestionDomain, vendorName, resourceName, apiVersion, ingestionRunId, requestId, pageNr);
        string metaBlobName    = ADLSBlobNamer.GetBlobName(BlobCategory.MetaData, environmentName, dataSourceIsExternal, ingestionDomain, vendorName, resourceName, apiVersion, ingestionRunId, requestId, pageNr);

        // Metadata stays as JSON text, so we encode here.
        var metaBytes = Encoding.UTF8.GetBytes(metaDataJson ?? string.Empty);

        await SaveBlob(container, contentBlobName, contentBytes, cancellationToken).ConfigureAwait(false);
        await SaveBlob(container, metaBlobName, metaBytes, cancellationToken).ConfigureAwait(false);
    }

    public static async Task SaveWatermark(BlobContainerClient container
                                         , string environmentName, bool dataSourceIsExternal, string ingestionDomain, string vendorName, string resourceName, int apiVersion
                                         , string watermarkJson
                                         , CancellationToken cancellationToken = default)
    {
        _ = container ?? throw new ArgumentNullException(nameof(container));

        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName, nameof(environmentName));
        ArgumentException.ThrowIfNullOrWhiteSpace(ingestionDomain, nameof(ingestionDomain));
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorName, nameof(vendorName));
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName, nameof(resourceName));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(apiVersion, nameof(apiVersion));

        string blobName = ADLSBlobNamer.GetBlobName(BlobCategory.Watermark, environmentName, dataSourceIsExternal, ingestionDomain, vendorName, resourceName, apiVersion);
        await SaveBlob(container, blobName, Encoding.UTF8.GetBytes(watermarkJson ?? string.Empty), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Lowest-level, flexible write: you hand us the final blobName.
    /// </summary>
    public static Task SaveBlob(BlobContainerClient container, string blobName, byte[] bytes, CancellationToken cancellationToken = default)
    {
        _ = container ?? throw new ArgumentNullException(nameof(container));
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName, nameof(blobName));
        _ = bytes ?? throw new ArgumentNullException(nameof(bytes));

        return UploadBytesAsync(container.GetBlobClient(blobName), bytes, cancellationToken);
    }

    private static async Task UploadBytesAsync(BlobClient blob, byte[] bytes, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream(bytes, writable: false);
        await blob.UploadAsync(ms, overwrite: true, cancellationToken).ConfigureAwait(false);
    }
}
