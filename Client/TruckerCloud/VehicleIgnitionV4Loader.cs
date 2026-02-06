using Azure.Storage.Blobs;
using Canal.Ingestion.ApiLoader.Engine.Adapters;
using Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud.Endpoints;
using System.Diagnostics.CodeAnalysis;
using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Client.TruckerCloud;

public class VehicleIgnitionV4Loader: EndpointLoaderBase
{
    [SetsRequiredMembers]
    public VehicleIgnitionV4Loader(IVendorAdapter vendorAdapter, BlobContainerClient containerClient, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs, bool override_constructor_exception = false) 
    : base(vendorAdapter, containerClient, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs)
    {
        if(override_constructor_exception == false) throw new InvalidOperationException("VehicleIgnitionV4Loader is intentinally disabled. When querying just 1 vehicle, the json that cvame back several minutes later was over a million lines long." );
    }

    /// <summary>
    /// Fetch Json From TruckerCloud Endpoint, optionally save results to ADLS
    /// </summary>
    /// <param name="pageSize">Pagination Parameter</param>
    /// <param name="vehicles">List of vehicles to iterate over</param>
    public async Task<List<FetchResult>> Load(List<FetchResult> vehicles, int pageSize = 500, int? maxNrPagesBeforeAbort = null, bool saveResults = true, CancellationToken cancellationToken = default)
    {
        var endpoint = new VehicleIgnitionV4Endpoint(VendorAdapter, EnvironmentName, MaxDegreeOfParallelism, MaxRetries, MinRetryDelayMs);
        InitRun(endpoint);
        var results = await endpoint.Fetch(IngestionRun, vehicles, pageSize, maxNrPagesBeforeAbort, cancellationToken).ConfigureAwait(false);
        if(saveResults) await SaveResultsAsync(results, cancellationToken).ConfigureAwait(false);
        return results;
    }
}

