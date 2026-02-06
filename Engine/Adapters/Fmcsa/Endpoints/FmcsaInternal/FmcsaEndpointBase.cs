using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa.Endpoints.Base;
internal abstract class FmcsaEndpointBase: EndpointBase, IEndpoint
{
    internal FmcsaEndpointBase(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    /// <summary>
    /// Fetch Json From Fmcsa Endpoint
    /// </summary>
    public async Task<List<FetchResult>> Fetch(IngestionRun? ingestionRun, int pageSize = 500, int? maxNrPagesBeforeAbort = null, CancellationToken cancellationToken = default)
    {
        var paginatedRequestSettings = new PaginatedRequestSettings(startIndex: 0, pageSize);
        if(maxNrPagesBeforeAbort.HasValue) paginatedRequestSettings.NrRequestsAllowedBeforeAbort = maxNrPagesBeforeAbort.Value;

        ArgumentNullException.ThrowIfNull(ingestionRun, nameof(ingestionRun));
        var request = new Request(_vendorAdapter, resourceName: ResourceName, resourceVersion: ResourceVersion, pagination: paginatedRequestSettings);
        var results = await _fetcher.ProcessRequest(ingestionRun, request, cancellationToken).ConfigureAwait(false);
        return results;        
    }    
}

