

/*
carrierCode
codeType
eldVendor

*/

using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud.Endpoints;
internal class VehicleIgnitionV4Endpoint: EndpointBase, IEndpoint
{
    public VehicleIgnitionV4Endpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}
    
    public override string ResourceName => "vehicles/ignition";
    public override int ResourceVersion => 4;

    /// <summary>
    /// Fetch Json From TruckerCloud Endpoint
    /// </summary>
    /// <param name="vehicles">FetchResults From Carrier Endpoint to be iterated over</param>
    public async Task<List<FetchResult>> Fetch(IngestionRun? ingestionRun, List<FetchResult> vehicles, int pageSize, int? maxNrPagesBeforeAbort = null, CancellationToken cancellationToken = default)
    {
        var paginatedRequestSettings = new PaginatedRequestSettings(startIndex: 0, pageSize);
        if(maxNrPagesBeforeAbort.HasValue) paginatedRequestSettings.NrRequestsAllowedBeforeAbort = maxNrPagesBeforeAbort.Value;
        
        ArgumentNullException.ThrowIfNull(ingestionRun, nameof(ingestionRun));
        ArgumentNullException.ThrowIfNull(vehicles, nameof(vehicles));

        var vehicleEldList = Internal.List_VehiclesCarrierCodesAndEldVendor.FromList(vehicles);

        var requests = vehicleEldList.Select(c =>
            new Request(_vendorAdapter, resourceName: ResourceName, resourceVersion: ResourceVersion, pagination: paginatedRequestSettings,
                        queryParameters: new Dictionary<string, string>
                        {
                            ["carrierCode"] = c.CarrierCode,
                            ["codeType"] = c.CodeType,
                            ["eldVendor"] = c.EldVendor,
                            ["vehicleId"] = c.VehicleId,
                            ["vehicleIdType"] = c.VehicleIdType
                        })
        ).ToList();

        var results = await _fetcher.ProcessRequests(ingestionRun, requests, cancellationToken).ConfigureAwait(false);
        return results;        
    }
}


