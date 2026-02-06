using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud.Endpoints;
internal class DriversV4Endpoint: EndpointBase, IEndpoint
{
    public DriversV4Endpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}
    
    public override string ResourceName => "drivers";
    public override int ResourceVersion => 4;

    /// <summary>
    /// Fetch Json From TruckerCloud Endpoint
    /// </summary>
    /// <param name="carriers">FetchResults From Carrier Endpoint to be iterated over</param>
    public async Task<List<FetchResult>> Fetch(IngestionRun? ingestionRun, List<FetchResult> carriers, int pageSize = 500, int? maxNrPagesBeforeAbort = null, CancellationToken cancellationToken = default)
    {
        var paginatedRequestSettings = new PaginatedRequestSettings(startIndex: 0, pageSize);
        if(maxNrPagesBeforeAbort.HasValue) paginatedRequestSettings.NrRequestsAllowedBeforeAbort = maxNrPagesBeforeAbort.Value;

        ArgumentNullException.ThrowIfNull(ingestionRun, nameof(ingestionRun));
        ArgumentNullException.ThrowIfNull(carriers, nameof(carriers));

        var carrierList = Internal.List_CarrierCodes.FromList(carriers);

        var requests = carrierList.Select(c =>
            new Request(_vendorAdapter, resourceName: ResourceName, resourceVersion: ResourceVersion, pagination: paginatedRequestSettings,
                        queryParameters: new Dictionary<string, string>
                        {
                            ["carrierCode"] = c.CarrierCode,
                            ["codeType"] = c.CarrierCodeType
                        })
        ).ToList();

        var results = await _fetcher.ProcessRequests(ingestionRun, requests, cancellationToken).ConfigureAwait(false);
        return results;        
    }
}

