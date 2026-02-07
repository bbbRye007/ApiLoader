using Azure.Storage.Blobs;
using Canal.Ingestion.ApiLoader.Engine;
using Canal.Ingestion.ApiLoader.Engine.Adapters;
using Canal.Ingestion.ApiLoader.Model;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Canal.Ingestion.ApiLoader.Client;

public class EndpointLoader : EndpointLoaderBase
{
    private readonly EndpointDefinition _definition;
    private readonly FetchEngine Fetcher;

    [SetsRequiredMembers]
    public EndpointLoader(EndpointDefinition definition, IVendorAdapter vendorAdapter, BlobContainerClient containerClient, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
        : base(vendorAdapter, containerClient, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Fetcher = new FetchEngine(vendorAdapter, maxDegreeOfParallelism, maxRetries, minRetryDelayMs);
    }

    public async Task<List<FetchResult>> Load(
        List<FetchResult>? priorResults = null, DateTimeOffset? overrideStartUtc = null, DateTimeOffset? overrideEndUtc = null,
        int? pageSize = null, int? maxNrPagesBeforeAbort = null, SaveBehavior saveBehavior = SaveBehavior.AfterAll, bool saveWatermark = true,
        string bodyParamsJson = "{}", CancellationToken cancellationToken = default)
    {
        InitRun(_definition);

        var effectivePageSize = pageSize ?? _definition.DefaultPageSize;
        var pagination = new PaginatedRequestSettings(startIndex: 0, effectivePageSize);
        if (maxNrPagesBeforeAbort.HasValue) pagination.NrRequestsAllowedBeforeAbort = maxNrPagesBeforeAbort.Value;

        // Resolve time window if this endpoint supports watermarking
        DateTimeOffset? startUtc = null, endUtc = null;
        if (_definition.SupportsWatermark)
        {
            var now = DateTimeOffset.UtcNow;
            var watermarkEnd = await GetEndUtcFromWatermarkAsync(cancellationToken).ConfigureAwait(false);
            startUtc = overrideStartUtc ?? watermarkEnd ?? now.AddDays(_definition.DefaultLookbackDays * -1);
            endUtc = overrideEndUtc ?? now;

            if (endUtc.Value - startUtc.Value < TimeSpan.FromHours(_definition.MinTimeSpanHours)) return [];
        }

        var parameters = new LoadParameters { PriorResults = priorResults, StartUtc = startUtc, EndUtc = endUtc, BodyParamsJson = bodyParamsJson };

        // The endpoint definition knows how to build its own requests
        var requests = _definition.BuildRequests(VendorAdapter, _definition, pagination, parameters);

        // When saving per-page, wire up a callback so the engine persists each page as it arrives.
        Func<FetchResult, Task>? onPageFetched = saveBehavior == SaveBehavior.PerPage
            ? async result => await SaveResultAsync(result, cancellationToken).ConfigureAwait(false)
            : null;

        var results = (requests.Count == 1)
            ? await Fetcher.ProcessRequest(IngestionRun!, requests[0], onPageFetched, cancellationToken).ConfigureAwait(false)
            : await Fetcher.ProcessRequests(IngestionRun!, requests, onPageFetched, cancellationToken).ConfigureAwait(false);

        if (saveBehavior == SaveBehavior.AfterAll) await SaveResultsAsync(results, cancellationToken).ConfigureAwait(false);
        if (saveWatermark && _definition.SupportsWatermark) await GenerateWatermarkAndSaveAsync(startUtc!.Value, endUtc!.Value, cancellationToken).ConfigureAwait(false);

        return results;
    }

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
            IngestionDurationMs = (long)(now - IngestionRun?.IngestionRunStartUtc ?? default).TotalMilliseconds
        });

        await SaveWatermarkAsync(watermarkJson, cancellationToken).ConfigureAwait(false);
    }

    private async Task<DateTimeOffset?> GetEndUtcFromWatermarkAsync(CancellationToken cancellationToken)
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
