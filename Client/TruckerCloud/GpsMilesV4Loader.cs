using Azure.Storage.Blobs;
using Canal.Ingestion.ApiLoader.Engine.Adapters;
using Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud.Endpoints;
using System.Diagnostics.CodeAnalysis;
using Canal.Ingestion.ApiLoader.Model;
using System.Text.Json;

namespace Canal.Ingestion.ApiLoader.Client.TruckerCloud;

public class GpsMilesV4Loader: EndpointLoaderBase
{

    [SetsRequiredMembers]
    public GpsMilesV4Loader(IVendorAdapter vendorAdapter, BlobContainerClient containerClient, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs) 
    : base(vendorAdapter, containerClient, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}


    /// <summary>
    /// Fetch Json From TruckerCloud Endpoint, optionally save results to ADLS
    /// </summary>
    /// <param name="pageSize">Pagination Parameter</param>
    public async Task<List<FetchResult>> Load(
        List<FetchResult> carriers, 
        DateTimeOffset? overrideStartUtc = null, 
        DateTimeOffset? overrideEndUtc = null, 
        int minTimeSpanHours = 12, 
        int defaultLookbackDays = 90, 
        int pageSize = 500, 
        int? maxNrPagesBeforeAbort = null,
        bool saveResults = true, 
        bool saveWatermark = true, 
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(defaultLookbackDays, nameof(defaultLookbackDays));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minTimeSpanHours, nameof(minTimeSpanHours));
        ArgumentNullException.ThrowIfNull(carriers);

        var now = DateTimeOffset.UtcNow;
        var endpoint = new GpsMilesV4Endpoint(VendorAdapter, EnvironmentName, MaxDegreeOfParallelism, MaxRetries, MinRetryDelayMs);
        InitRun(endpoint);        

        DateTimeOffset? warterMarkEndUtc = await GetEndUtcFromWatermark_Async(cancellationToken).ConfigureAwait(false);;
        DateTimeOffset defaultStartUtc = warterMarkEndUtc ?? now.AddDays(defaultLookbackDays * -1);
        DateTimeOffset startUtc = overrideStartUtc ?? defaultStartUtc;
        DateTimeOffset endUtc = overrideEndUtc ?? now;

        TimeSpan requiredGap = TimeSpan.FromHours(minTimeSpanHours);

        List<FetchResult> results = [];

        if(endUtc - startUtc >= requiredGap) 
        {
            results = await endpoint.Fetch(IngestionRun, carriers, startUtc, endUtc, pageSize, maxNrPagesBeforeAbort, cancellationToken).ConfigureAwait(false);
            if(saveResults) await SaveResultsAsync(results, cancellationToken).ConfigureAwait(false);
            if(saveWatermark) await GenerateWatermarkAndSaveAsync(startUtc, endUtc, cancellationToken).ConfigureAwait(false);
        }
        return results;
    }

    // MAYBE TODO: Add flavors
    // public async Task<List<FetchResult>> LoadSpecificDates -- pass in start and end date overrides
    // public async Task<List<FetchResult>> Load -- pass no deae, just runs incrementally.

	private async Task GenerateWatermarkAndSaveAsync(DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
		var watermarkJson = JsonSerializer.Serialize(new
		{
			StartTimeUtc = startUtc.ToUniversalTime(),
			EndTimeUtc = endUtc.ToUniversalTime(),
			IngestionRunId = IngestionRun?.IngestionRunId ?? string.Empty,
			IngestionRunStartDts = IngestionRun?.IngestionRunStartUtc.ToUniversalTime(),
			WrittenUtc = now,
            IngestionDurationMs = (long) (now - IngestionRun?.IngestionRunStartUtc ?? default).TotalMilliseconds
		});

        await SaveWatermarkAsync(watermarkJson, cancellationToken).ConfigureAwait(false);
    }


	private async Task<DateTimeOffset?> GetEndUtcFromWatermark_Async(CancellationToken cancellationToken) 
    {
        
        DateTimeOffset? endUtc = null;

        using (var watermarkJson = await LoadWatermarkAsync(cancellationToken).ConfigureAwait(false))
        {
            if (watermarkJson is not null
                && watermarkJson.RootElement.ValueKind == JsonValueKind.Object
                && watermarkJson.RootElement.TryGetProperty("EndTimeUtc", out var endProp)
                && endProp.ValueKind == JsonValueKind.String
                && endProp.TryGetDateTimeOffset(out var lastEndUtc))
            {
                endUtc = lastEndUtc.ToUniversalTime().AddSeconds(1); // 1 second after previous end time
            }
        }

        return endUtc;
    }    
}

