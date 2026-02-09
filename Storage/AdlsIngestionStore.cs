using System.Text.Json;
using Azure.Storage.Blobs;
using Canal.Storage.Adls;

namespace Canal.Ingestion.ApiLoader.Storage;

/// <summary>
/// ADLS-backed implementation of <see cref="IIngestionStore"/>.
/// Thin wrapper that delegates to the existing static ADLSWriter / ADLSReader / ADLSBlobNamer helpers.
/// </summary>
public sealed class AdlsIngestionStore : IIngestionStore
{
    private readonly BlobContainerClient _container;

    public AdlsIngestionStore(BlobContainerClient container)
        => _container = container ?? throw new ArgumentNullException(nameof(container));

    public Task SaveResultAsync(
        IngestionCoordinates coords,
        string ingestionRunId,
        string requestId,
        int pageNr,
        string contentJson,
        string metaDataJson,
        CancellationToken cancellationToken = default)
    {
        return ADLSWriter.SavePayloadAndMetadata(
            container: _container,
            environmentName: coords.EnvironmentName,
            dataSourceIsExternal: coords.IsExternalSource,
            ingestionDomain: coords.IngestionDomain,
            vendorName: coords.VendorName,
            resourceName: coords.ResourceName,
            apiVersion: coords.ResourceVersion,
            ingestionRunId: ingestionRunId,
            requestId: requestId,
            pageNr: pageNr,
            contentJson: contentJson,
            metaDataJson: metaDataJson,
            cancellationToken: cancellationToken);
    }

    public Task SaveWatermarkAsync(
        IngestionCoordinates coords,
        string watermarkJson,
        CancellationToken cancellationToken = default)
    {
        return ADLSWriter.SaveWatermark(
            container: _container,
            environmentName: coords.EnvironmentName,
            dataSourceIsExternal: coords.IsExternalSource,
            ingestionDomain: coords.IngestionDomain,
            vendorName: coords.VendorName,
            resourceName: coords.ResourceName,
            apiVersion: coords.ResourceVersion,
            watermarkJson: watermarkJson,
            cancellationToken: cancellationToken);
    }

    public Task<JsonDocument?> LoadWatermarkAsync(
        IngestionCoordinates coords,
        CancellationToken cancellationToken = default)
    {
        string watermarkPath = ADLSBlobNamer.GetBlobName(
            BlobCategory.Watermark,
            coords.EnvironmentName,
            coords.IsExternalSource,
            coords.IngestionDomain,
            coords.VendorName,
            coords.ResourceName,
            coords.ResourceVersion);

        return ADLSReader.GetBlobAsJsonAsync(_container, watermarkPath, cancellationToken);
    }
}
