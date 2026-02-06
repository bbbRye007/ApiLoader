using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud.Endpoints;
internal class SafetyEventsV5Endpoint: EndpointBase, IEndpoint
{
    public SafetyEventsV5Endpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    public override string ResourceName => "safety-events";
    public override int ResourceVersion => 5;
    /// <summary>
    /// Fetch Json From TruckerCloud Endpoint
    /// </summary>
    /// <param name="carriers">FetchResults From Carrier Endpoint to be iterated over</param>
    /// <param name="warterMarkEndUtc">End TimeStamp From Previous Incremental Run</param>
    /// <param name="overrideStartUtc">Optional Override to Query Param: startDateTime. Default Behavior: startDateTime = warterMarkEndUtc ?? now.AddDays(defaultLookbackDays * -1)</param>
    /// <param name="overrideEndUtc">Optional Override to Query Param: endDateTime. Default Behavior: endDateTime = now.AddDays(defaultLookbackDays * -1)</param>
    /// <param name="minTimeSpanHours">If difference between startDateTime and endDateTime parameters is less than this number of hours, method returns an empty List</param>
    /// <param name="defaultLookbackDays">If no waterMarkEndUtc is available and no overrideStartUtc was provided, startDateTime is set to now.AddDays(defaultLookbackDays * -1)</param>
    public async Task<List<FetchResult>> Fetch(IngestionRun? ingestionRun,List<FetchResult> carriers, DateTimeOffset startUtc, DateTimeOffset endUtc, int pageSize = 500
                                             , int? maxNrPagesBeforeAbort = null, string bodyParamsJson = "{}", CancellationToken cancellationToken = default)
    {
        var paginatedRequestSettings = new PaginatedRequestSettings(startIndex: 0, pageSize);
        if(maxNrPagesBeforeAbort.HasValue) paginatedRequestSettings.NrRequestsAllowedBeforeAbort = maxNrPagesBeforeAbort.Value;
    
        ArgumentNullException.ThrowIfNull(ingestionRun, nameof(ingestionRun));
        ArgumentNullException.ThrowIfNull(carriers, nameof(carriers));

        var carrierEldList = Internal.List_CarrierCodesAndEldVendors.FromList(carriers).Where(c=> c.EldVendor != "Motive").OrderBy(o=> o.EldVendor); 

        var requests = carrierEldList.Select(c =>
            new Request(_vendorAdapter, resourceName: ResourceName, resourceVersion: ResourceVersion, httpMethod: HttpMethod.Post, bodyParamsJson: bodyParamsJson, pagination: paginatedRequestSettings,
                        queryParameters: new Dictionary<string, string>
                        {
                            ["carrierCode"] = c.CarrierCode,
                            ["codeType"] = c.CarrierCodeType,
                            ["eldVendor"] = c.EldVendor,
                            ["startTime"] = startUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
                            ["endTime"] = endUtc.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'")
                        })
        ).ToList();

        var results = await _fetcher.ProcessRequests(ingestionRun, requests, cancellationToken).ConfigureAwait(false);
        return results;
    }
}
