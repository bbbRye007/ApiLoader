using Canal.Ingestion.ApiLoader.Adapters;
using System.Diagnostics.CodeAnalysis;
using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Storage;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Client;
public abstract class EndpointLoaderBase
{

    [SetsRequiredMembers]
    public EndpointLoaderBase(IVendorAdapter vendorAdapter, IIngestionStore store, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs, ILoggerFactory loggerFactory)
    {
        VendorAdapter = vendorAdapter;
        Store = store;
        EnvironmentName = environmentName;
        MaxDegreeOfParallelism = maxDegreeOfParallelism;
        MaxRetries = maxRetries;
        MinRetryDelayMs = minRetryDelayMs;
        LoggerFactory = loggerFactory;
        Logger = loggerFactory.CreateLogger<EndpointLoaderBase>();
    }
    protected IVendorAdapter VendorAdapter { get; }
    protected IIngestionStore Store { get; init; }
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

    /// <summary>
    /// Builds the <see cref="IngestionCoordinates"/> that identify this endpoint's location in storage.
    /// </summary>
    protected IngestionCoordinates GetCoordinates(string resourceName, int resourceVersion)
        => new(EnvironmentName, IsExternalSource, IngestionDomain, VendorName, resourceName, resourceVersion);

    public async Task SaveResultsAsync(List<FetchResult> fetchResults, CancellationToken cancellationToken)
    {
        foreach (var r in fetchResults)
            await SaveResultAsync(r, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveResultAsync(FetchResult r, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Saving result for {Endpoint} v{Version} page {PageNr} (run {RunId})",
            r.Request.ResourceNameFriendly, r.Request.ResourceVersion, r.PageNr, r.IngestionRun.IngestionRunId);

        var coords = GetCoordinates(r.Request.ResourceNameFriendly, r.Request.ResourceVersion);

        await Store.SaveResultAsync(
            coords: coords,
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

        var coords = GetCoordinates(ResourceName, ResourceVersion);

        await Store.SaveWatermarkAsync(coords, watermarkJson, cancellationToken).ConfigureAwait(false);
    }

    public async Task<JsonDocument?> LoadWatermarkAsync(CancellationToken cancellationToken)
    {
        var coords = GetCoordinates(ResourceName, ResourceVersion);

        Logger.LogDebug("Loading watermark for {Vendor}/{Endpoint} v{Version}", VendorName, ResourceName, ResourceVersion);

        var watermarkJson = await Store.LoadWatermarkAsync(coords, cancellationToken).ConfigureAwait(false);

        if (watermarkJson is null)
            Logger.LogDebug("No existing watermark found for {Vendor}/{Endpoint} v{Version}", VendorName, ResourceName, ResourceVersion);

        return watermarkJson;
    }

}
