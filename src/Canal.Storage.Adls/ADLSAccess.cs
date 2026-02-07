using Azure.Core;
using Azure.Storage.Blobs;

namespace Canal.Storage.Adls;

/// <summary>
/// Minimal wrapper around a BlobContainerClient.
///
/// Key design goals:
/// - No config loading.
/// - No secrets.
/// - No singletons/statics (avoid hidden global state that complicates lifetimes/disposal or complicates management of state across runs/tests or becomes a "foot gun" in a parallelized/retry environment)
/// - Host (console app / container / API) owns composition of secrets and config params).
/// </summary>
public interface IBlobContainerClientProvider
{
    BlobContainerClient ContainerClient { get; }
}

public sealed class ADLSAccess : IBlobContainerClientProvider
{
    public BlobContainerClient ContainerClient { get; }

    public ADLSAccess(BlobContainerClient containerClient)
    {
        ContainerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
    }

    /// <summary>
    /// Creates a container client using standard "https://{account}.blob.core.windows.net/{container}" addressing.
    /// Prefer this when you know account + container name.
    /// </summary>
    public static ADLSAccess Create(string accountName, string containerName, TokenCredential credential)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(accountName, nameof(accountName));
        ArgumentException.ThrowIfNullOrWhiteSpace(containerName, nameof(containerName));
        _ = credential ?? throw new ArgumentNullException(nameof(credential));

        // Note: BlobContainerClient expects a container URI.
        var containerUri = new Uri($"https://{accountName.Trim()}.blob.core.windows.net/{containerName.Trim()}");
        return new ADLSAccess(new BlobContainerClient(containerUri, credential));
    }

    /// <summary>
    /// Creates a container client from a container-level URI.
    /// Example: https://myacct.blob.core.windows.net/mycontainer
    /// </summary>
    public static ADLSAccess Create(Uri containerUri, TokenCredential credential)
    {
        _ = containerUri ?? throw new ArgumentNullException(nameof(containerUri));
        _ = credential ?? throw new ArgumentNullException(nameof(credential));

        return new ADLSAccess(new BlobContainerClient(containerUri, credential));
    }
}
