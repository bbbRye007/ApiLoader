using Azure.Storage.Blobs;
using Canal.Ingestion.ApiLoader.Engine.Adapters;
using System.Diagnostics.CodeAnalysis;
using Canal.Ingestion.ApiLoader.Model;
using Canal.Storage.Adls;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Client;
public abstract class EndpointLoaderBase
{

    [SetsRequiredMembers]
    public EndpointLoaderBase(IVendorAdapter vendorAdapter, BlobContainerClient containerClient, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs, ILoggerFactory loggerFactory)
    {
        VendorAdapter = vendorAdapter;
        ContainerClient = containerClient;
        EnvironmentName = environmentName;
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
        MaxRetries = maxRetries;
        MinRetryDelayMs = minRetryDelayMs;
        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger<EndpointLoaderBase>();
    }
    protected IVendorAdapter VendorAdapter { get; }
    protected BlobContainerClient ContainerClient { get; init; }
    protected string EnvironmentName { get; init; }
    protected int MaxDegreeOfParallelism { get; init; }
    protected int MaxRetries { get; init; }
    protected int MinRetryDelayMs { get; init; }
    protected ILoggerFactory LoggerFactory { get; init; }
    protected ILogger Logger { get; init; }

    internal EndpointDefinition? Definition { get; private set; } = null;
    public string IngestionDomain => VendorAdapter?.IngestionDomain ?? string.Empty;
    public string VendorName => VendorAdapter?.VendorName ?? string.Empty;
    public bool IsExternalSource => VendorAdapter?.IsExternalSource ?? true;
    public string ResourceName => Definition?.ResourceName ?? string.Empty;
    public int ResourceVersion => Definition?.ResourceVersion ?? 0;
    public IngestionRun? IngestionRun { get; protected set; }

    internal void InitRun(EndpointDefinition definition)
    {
        IngestionRun = new(EnvironmentName, IngestionDomain, VendorName);
        Definition = definition;
    }

    public async Task SaveResultsAsync(List<FetchResult> fetchResults, CancellationToken cancellationToken)
    {
        foreach (var r in fetchResults)
            await SaveResultAsync(r, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveResultAsync(FetchResult r, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Saving result for {Endpoint} v{Version} page {PageNr} (run {RunId})",
            r.Request.ResourceNameFriendly, r.Request.ResourceVersion, r.PageNr, r.IngestionRun.IngestionRunId);

        await ADLSWriter.SavePayloadAndMetadata(
            container: ContainerClient,
            environmentName: r.IngestionRun.EnvironmentName,
            dataSourceIsExternal: IsExternalSource,
            ingestionDomain: r.IngestionRun.IngestionDomain,
            vendorName: r.IngestionRun.VendorName,
            resourceName: r.Request.ResourceNameFriendly,
            apiVersion: r.Request.ResourceVersion,
            ingestionRunId: r.IngestionRun.IngestionRunId,
            requestId: r.Request.RequestId,
            pageNr: r.PageNr,
            contentJson: r.Content ?? string.Empty,
            metaDataJson: r.MetaDataJson,
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    public async Task SaveWatermarkAsync(string watermarkJson, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Saving watermark for {Vendor}/{Endpoint} v{Version}", VendorName, ResourceName, ResourceVersion);

        await ADLSWriter.SaveWatermark(ContainerClient, EnvironmentName, IsExternalSource, IngestionDomain, VendorName, ResourceName, ResourceVersion, watermarkJson, cancellationToken)
                        .ConfigureAwait(false);
    }

    public async Task<JsonDocument?> LoadWatermarkAsync(CancellationToken cancellationToken)
    {
        string watermarkPath = ADLSBlobNamer.GetBlobName(BlobCategory.Watermark, EnvironmentName, IsExternalSource, IngestionDomain, VendorName, ResourceName, ResourceVersion);

        Logger.LogDebug("Loading watermark from {WatermarkPath}", watermarkPath);

        var watermarkJson = await ADLSReader.GetBlobAsJsonAsync(ContainerClient, watermarkPath, cancellationToken).ConfigureAwait(false);

        if (watermarkJson is null)
            Logger.LogDebug("No existing watermark found at {WatermarkPath}", watermarkPath);

        return watermarkJson;
    }

}
