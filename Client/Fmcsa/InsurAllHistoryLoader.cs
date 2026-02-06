using Azure.Storage.Blobs;
using Canal.Ingestion.ApiLoader.Engine.Adapters;
using Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa.Endpoints;
using System.Diagnostics.CodeAnalysis;
using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Client.Fmcsa;
public class InsurAllHistoryLoader: EndpointLoaderBase
{

    [SetsRequiredMembers]
    public InsurAllHistoryLoader(IVendorAdapter vendorAdapter, BlobContainerClient containerClient, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs) 
    : base(vendorAdapter, containerClient, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    /// <summary>
    /// Fetch Json From TruckerCloud Endpoint, optionally save results to ADLS
    /// </summary>
    /// <param name="pageSize">Pagination Parameter</param>
    public async Task<List<FetchResult>> Load(int pageSize = 500, bool saveResults = true, int? maxNrPagesBeforeAbort = null, CancellationToken cancellationToken = default)
    {
        var endpoint = new InsurAllHistoryEndpoint(VendorAdapter, EnvironmentName, MaxDegreeOfParallelism, MaxRetries, MinRetryDelayMs);
        InitRun(endpoint);
        var results = await endpoint.Fetch(IngestionRun, pageSize, maxNrPagesBeforeAbort, cancellationToken).ConfigureAwait(false);
        if(saveResults) await SaveResultsAsync(results, cancellationToken).ConfigureAwait(false);
        return results;
    }
}