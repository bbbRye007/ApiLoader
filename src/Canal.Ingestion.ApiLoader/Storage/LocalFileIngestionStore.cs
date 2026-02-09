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

    /// <summary>
    /// The longest path below the root is roughly:
    ///   {env}/{ext}/{domain}/{vendor}/{resource}/{version}/{runId}/metadata/metadata_{reqId12}_p0001.json
    /// which is ~170 chars. Keeping the root under 80 leaves headroom for MAX_PATH (260).
    /// </summary>
    private const int MaxRootPathLength = 80;

    /// <summary>
    /// Full SHA256 hex is 64 chars — overkill for a local dev folder.
    /// 12 hex chars (48 bits) is more than enough to avoid collisions within a single run.
    /// </summary>
    private const int RequestIdMaxLength = 12;

    public LocalFileIngestionStore(string rootFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolder, nameof(rootFolder));

        var fullPath = Path.GetFullPath(rootFolder);
        if (fullPath.Length > MaxRootPathLength)
            throw new ArgumentException(
                $"Root folder path is {fullPath.Length} characters, which exceeds the {MaxRootPathLength}-character limit. " +
                $"Use a shorter path to avoid Windows MAX_PATH (260) issues. Path: {fullPath}",
                nameof(rootFolder));

        _rootFolder = fullPath;
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
        var shortRequestId = TruncateRequestId(requestId);
        var versionStr = FormatVersion(coords.ResourceVersion);
        var pageStr = FormatPage(pageNr);

        var dataFolder = Path.Combine(BuildResourceFolder(coords, versionStr), ingestionRunId);
        var metaFolder = Path.Combine(dataFolder, "metadata");
        Directory.CreateDirectory(dataFolder);
        Directory.CreateDirectory(metaFolder);

        await File.WriteAllTextAsync(
            Path.Combine(dataFolder, $"data_{shortRequestId}_p{pageStr}.json"),
            contentJson ?? string.Empty, cancellationToken).ConfigureAwait(false);

        await File.WriteAllTextAsync(
            Path.Combine(metaFolder, $"metadata_{shortRequestId}_p{pageStr}.json"),
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

    private static string TruncateRequestId(string requestId)
    {
        if (string.IsNullOrEmpty(requestId)) return "unknown";
        return requestId.Length <= RequestIdMaxLength ? requestId : requestId[..RequestIdMaxLength];
    }

    private static string FormatVersion(int version) => version.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
    private static string FormatPage(int pageNr) => pageNr.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
}
