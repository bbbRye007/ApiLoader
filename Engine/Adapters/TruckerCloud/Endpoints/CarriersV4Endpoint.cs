using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud.Endpoints;
internal class CarriersV4Endpoint: EndpointBase, IEndpoint
{
    internal CarriersV4Endpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    public override string ResourceName => "carriers";
    public override int ResourceVersion => 4;

    /// <summary>
    /// Fetch Json From TruckerCloud Endpoint
    /// </summary>
    /// <param name="pageSize">Pagination Parameter</param>
    internal async Task<List<FetchResult>> Fetch(IngestionRun? ingestionRun, int pageSize = 500, int? maxNrPagesBeforeAbort = null, CancellationToken cancellationToken = default)
    {
        var paginatedRequestSettings = new PaginatedRequestSettings(startIndex: 0, pageSize);
        if(maxNrPagesBeforeAbort.HasValue) paginatedRequestSettings.NrRequestsAllowedBeforeAbort = maxNrPagesBeforeAbort.Value;

        ArgumentNullException.ThrowIfNull(ingestionRun, nameof(ingestionRun));
        var request = new Request(_vendorAdapter, resourceName: ResourceName, resourceVersion: ResourceVersion, pagination: paginatedRequestSettings);
        var results = await _fetcher.ProcessRequest(ingestionRun, request, cancellationToken).ConfigureAwait(false);
        return results;        
    }
}

