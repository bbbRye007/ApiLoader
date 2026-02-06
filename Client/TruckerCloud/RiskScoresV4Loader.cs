using Azure.Storage.Blobs;
using Canal.Ingestion.ApiLoader.Engine.Adapters;
using Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud.Endpoints;
using System.Diagnostics.CodeAnalysis;
using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Client.TruckerCloud;
public class RiskScoresV4Loader: EndpointLoaderBase
{

    [SetsRequiredMembers]
    public RiskScoresV4Loader(IVendorAdapter vendorAdapter, BlobContainerClient containerClient, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs) 
    : base(vendorAdapter, containerClient, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    /// <summary>
    /// Fetch Json From TruckerCloud Endpoint, optionally save results to ADLS
    /// </summary>
    /// <param name="pageSize">Pagination Parameter</param>
    public async Task<List<FetchResult>> Load(List<FetchResult> carriers, int pageSize = 500, int? maxNrPagesBeforeAbort = null, bool saveResults = true, CancellationToken cancellationToken = default)
    {
        var endpoint = new RiskScoresV4Endpoint(VendorAdapter, EnvironmentName, MaxDegreeOfParallelism, MaxRetries, MinRetryDelayMs);
        InitRun(endpoint);
        var results = await endpoint.Fetch(IngestionRun, carriers, pageSize, maxNrPagesBeforeAbort, cancellationToken).ConfigureAwait(false);
        if(saveResults) await SaveResultsAsync(results, cancellationToken).ConfigureAwait(false);
        return results;
    }
}

