using System.Globalization;
using System.Text.Json;

namespace Canal.Ingestion.ApiLoader.Storage;

/// <summary>
/// Writes ingestion payloads, metadata, and watermarks to a local folder.
/// Mirrors the ADLS blob path hierarchy so the output structure looks familiar.
/// Useful for local dev / debugging without Azure credentials.
/// </summary>
public sealed class LocalFileIngestionStore : IIngestionStore
{
    private readonly string _rootFolder;

    public LocalFileIngestionStore(string rootFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder, nameof(rootFolder));
        _rootFolder = Path.GetFullPath(rootFolder);
    }

    public async Task SaveResultAsync(
        IngestionCoordinates coords,
        string ingestionRunId,
        string requestId,
        int pageNr,
        string contentJson,
        string metaDataJson,
        CancellationToken cancellationToken = default)
    {
        var versionStr = FormatVersion(coords.ResourceVersion);
        var pageStr = FormatPage(pageNr);

        var dataFolder = Path.Combine(BuildResourceFolder(coords, versionStr), ingestionRunId);
        var metaFolder = Path.Combine(dataFolder, "metadata");
        Directory.CreateDirectory(dataFolder);
        Directory.CreateDirectory(metaFolder);

        await File.WriteAllTextAsync(
            Path.Combine(dataFolder, $"data_{requestId}_p{pageStr}.json"),
            contentJson ?? string.Empty, cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(metaFolder, $"metadata_{requestId}_p{pageStr}.json"),
            metaDataJson ?? string.Empty, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveWatermarkAsync(
        IngestionCoordinates coords,
        string watermarkJson,
        CancellationToken cancellationToken = default)
    {
        var folder = BuildResourceFolder(coords, FormatVersion(coords.ResourceVersion));
        Directory.CreateDirectory(folder);

        await File.WriteAllTextAsync(
            Path.Combine(folder, "ingestion_watermark.json"),
            watermarkJson ?? string.Empty, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonDocument?> LoadWatermarkAsync(
        IngestionCoordinates coords,
        CancellationToken cancellationToken = default)
    {
        var folder = BuildResourceFolder(coords, FormatVersion(coords.ResourceVersion));
        var path = Path.Combine(folder, "ingestion_watermark.json");

        if (!File.Exists(path))
            return null;

        var stream = File.OpenRead(path);
        await using (stream.ConfigureAwait(false))
        {
            try
            {
                return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Builds: {root}/{env}/{internal|external}/{domain}/{vendor}/{resource}/{version}
    /// </summary>
    private string BuildResourceFolder(IngestionCoordinates coords, string versionStr)
    {
        var internalExternal = coords.IsExternalSource ? "external" : "internal";
        return Path.Combine(_rootFolder, coords.EnvironmentName, internalExternal, coords.IngestionDomain, coords.VendorName, coords.ResourceName, versionStr);
    }

    private static string FormatVersion(int version) => version.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
    private static string FormatPage(int pageNr) => pageNr.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
}
