using System.Text.Json;

namespace Canal.Ingestion.ApiLoader.Storage;

/// <summary>
/// Coordinates that identify where ingestion data lives in storage.
/// Groups the parameters that always travel together across save/load operations.
/// </summary>
public record IngestionCoordinates(
    string EnvironmentName,
    bool IsExternalSource,
    string IngestionDomain,
    string VendorName,
    string ResourceName,
    int ResourceVersion);

/// <summary>
/// Abstracts persistence for ingestion payloads, metadata, and watermarks.
/// The core engine depends on this interface; concrete implementations (ADLS, local filesystem, etc.) are wired up by the host.
/// </summary>
public interface IIngestionStore
{
    Task SaveResultAsync(
        IngestionCoordinates coords,
        string ingestionRunId,
        string requestId,
        int pageNr,
        string contentJson,
        string metaDataJson,
        CancellationToken cancellationToken = default);

    Task SaveWatermarkAsync(
        IngestionCoordinates coords,
        string watermarkJson,
        CancellationToken cancellationToken = default);

    Task<JsonDocument?> LoadWatermarkAsync(
        IngestionCoordinates coords,
        CancellationToken cancellationToken = default);
}
