using Azure;
using Azure.Storage.Blobs;
using System.Text;
using System.Text.Json;

namespace Canal.Storage.Adls;

public static class ADLSReader
{
    public static async Task<byte[]> GetBlobAsync(BlobContainerClient container, string blobPath, CancellationToken cancellationToken = default)
    {
        _ = container ?? throw new ArgumentNullException(nameof(container));
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath, nameof(blobPath));

        var normalizedPath = blobPath.Trim().TrimStart('/');
        var blob = container.GetBlobClient(normalizedPath);

        try
        {
            var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await using var content = download.Value.Content;

            using var ms = new MemoryStream();
            await content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
            return ms.ToArray();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return Array.Empty<byte>();
        }
    }

    public static async Task<string> GetBlobAsStringAsync(BlobContainerClient container, string blobPath, CancellationToken cancellationToken = default)
    {
        _ = container ?? throw new ArgumentNullException(nameof(container));
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath, nameof(blobPath));

        var normalizedPath = blobPath.Trim().TrimStart('/');
        var blob = container.GetBlobClient(normalizedPath);

        try
        {
            var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await using var content = download.Value.Content;

            using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, bufferSize: 1024 * 8, leaveOpen: false);
            return await reader.ReadToEndAsync().ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return string.Empty;
        }
    }

    public static async Task<JsonDocument?> GetBlobAsJsonAsync(BlobContainerClient container, string blobPath, CancellationToken cancellationToken = default)
    {
        _ = container ?? throw new ArgumentNullException(nameof(container));
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath, nameof(blobPath));

        var normalizedPath = blobPath.Trim().TrimStart('/');
        var blob = container.GetBlobClient(normalizedPath);

        try
        {
            var download = await blob.DownloadStreamingAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            await using var content = download.Value.Content;

            return await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
