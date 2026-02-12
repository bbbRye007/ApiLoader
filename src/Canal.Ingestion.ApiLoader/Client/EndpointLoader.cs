using Canal.Ingestion.ApiLoader.Engine;
using Canal.Ingestion.ApiLoader.Adapters;
using Canal.Ingestion.ApiLoader.Events;
using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Storage;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Client;

public class EndpointLoader : EndpointLoaderBase
{
    private readonly EndpointDefinition _definition;
    private readonly FetchEngine Fetcher;

    [SetsRequiredMembers]
    public EndpointLoader(EndpointDefinition definition, IVendorAdapter vendorAdapter, IIngestionStore store, IEventPublisher eventPublisher, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs, ILoggerFactory loggerFactory)
        : base(vendorAdapter, store, eventPublisher, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs, loggerFactory)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        Fetcher = new FetchEngine(vendorAdapter, maxDegreeOfParallelism, maxRetries, minRetryDelayMs, loggerFactory.CreateLogger<FetchEngine>());
    }

    public async Task<List<FetchResult>> Load(
        List<FetchResult>? iterationList = null, DateTimeOffset? overrideStartUtc = null, DateTimeOffset? overrideEndUtc = null,
        int? pageSize = null, int? maxPages = null, SaveBehavior saveBehavior = SaveBehavior.AfterAll, bool saveWatermark = true,
        string bodyParamsJson = "{}", CancellationToken cancellationToken = default)
    {
        InitRun(_definition);

        var subject = $"{VendorName}/{_definition.FriendlyName}";
        var eventSource = $"/canal/ingestion/apiloader/{EnvironmentName}";

        Logger.LogInformation("Load started: {Vendor}/{Endpoint} v{Version} (run {RunId}, saveBehavior={SaveBehavior})",
            VendorName, _definition.FriendlyName, _definition.ResourceVersion, IngestionRun!.IngestionRunId, saveBehavior);

        await EventPublisher.PublishAsync(new IngestionEvent
        {
            Type = IngestionEventTypes.RunStarted,
            Source = eventSource,
            Subject = subject,
            Time = IngestionRun.IngestionRunStartUtc,
            Data = new Dictionary<string, object>
            {
                ["run_id"] = IngestionRun.IngestionRunId,
                ["vendor"] = VendorName,
                ["endpoint"] = _definition.FriendlyName,
                ["resource_version"] = _definition.ResourceVersion,
                ["environment"] = EnvironmentName,
                ["save_behavior"] = saveBehavior.ToString()
            }
        }, cancellationToken).ConfigureAwait(false);

        if (_definition.RequiresIterationList && (iterationList is null || iterationList.Count == 0))
            throw new ArgumentException(
                $"Endpoint '{_definition.FriendlyName}' requires an iteration list (e.g., carrier results from a prior Load() call). " +
                "Pass the output as the iterationList parameter.",
                nameof(iterationList));

        var effectivePageSize = pageSize ?? _definition.DefaultPageSize;

        // Resolve time window if this endpoint supports watermarking
        DateTimeOffset? startUtc = null, endUtc = null;
        if (_definition.SupportsWatermark)
        {
            var now = DateTimeOffset.UtcNow;
            var watermarkEnd = await GetEndUtcFromWatermarkAsync(cancellationToken).ConfigureAwait(false);
            startUtc = overrideStartUtc ?? watermarkEnd ?? now.AddDays(_definition.DefaultLookbackDays * -1);
            endUtc = overrideEndUtc ?? now;

            Logger.LogInformation("Time window for {Endpoint}: {StartUtc} to {EndUtc}", _definition.FriendlyName, startUtc, endUtc);

            var timeSpan = endUtc.Value - startUtc.Value;
            if (_definition.MinTimeSpan.HasValue && timeSpan < _definition.MinTimeSpan.Value)
            {
                Logger.LogInformation("Time span {TimeSpan} below minimum {MinTimeSpan} for {Endpoint}, skipping", timeSpan, _definition.MinTimeSpan.Value, _definition.FriendlyName);
                return [];
            }
            if (_definition.MaxTimeSpan.HasValue && timeSpan > _definition.MaxTimeSpan.Value)
                endUtc = startUtc.Value + _definition.MaxTimeSpan.Value;
        }

        var parameters = new LoadParameters { IterationList = iterationList, StartUtc = startUtc, EndUtc = endUtc, BodyParamsJson = bodyParamsJson };

        // The endpoint definition knows how to build its own requests
        var requests = _definition.BuildRequests(VendorAdapter, _definition, effectivePageSize, parameters);

        Logger.LogInformation("Built {RequestCount} seed request(s) for {Endpoint}", requests.Count, _definition.FriendlyName);

        // Apply the safety cap to every seed request
        if (maxPages.HasValue)
            foreach (var r in requests) r.MaxPages = maxPages.Value;

        // When saving per-page, wire up a callback so the engine persists each page as it arrives.
        Func<FetchResult, Task>? onPageFetched = saveBehavior == SaveBehavior.PerPage
            ? async result => await SaveResultAsync(result, cancellationToken).ConfigureAwait(false)
            : null;

        List<FetchResult> results;
        try
        {
            results = (requests.Count == 1)
                ? await Fetcher.ProcessRequest(IngestionRun!, requests[0], onPageFetched, cancellationToken).ConfigureAwait(false)
                : await Fetcher.ProcessRequests(IngestionRun!, requests, onPageFetched, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await EventPublisher.PublishAsync(new IngestionEvent
            {
                Type = IngestionEventTypes.RunFailed,
                Source = eventSource,
                Subject = subject,
                Time = DateTimeOffset.UtcNow,
                Data = new Dictionary<string, object>
                {
                    ["run_id"] = IngestionRun.IngestionRunId,
                    ["vendor"] = VendorName,
                    ["endpoint"] = _definition.FriendlyName,
                    ["resource_version"] = _definition.ResourceVersion,
                    ["environment"] = EnvironmentName,
                    ["error"] = ex.Message
                }
            }, cancellationToken).ConfigureAwait(false);

            await EventPublisher.FlushAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        if (saveBehavior == SaveBehavior.AfterAll) await SaveResultsAsync(results, cancellationToken).ConfigureAwait(false);

        if (saveWatermark && _definition.SupportsWatermark)
        {
            await GenerateWatermarkAndSaveAsync(startUtc!.Value, endUtc!.Value, cancellationToken).ConfigureAwait(false);

            await EventPublisher.PublishAsync(new IngestionEvent
            {
                Type = IngestionEventTypes.WatermarkAdvanced,
                Source = eventSource,
                Subject = subject,
                Time = DateTimeOffset.UtcNow,
                Data = new Dictionary<string, object>
                {
                    ["run_id"] = IngestionRun.IngestionRunId,
                    ["vendor"] = VendorName,
                    ["endpoint"] = _definition.FriendlyName,
                    ["resource_version"] = _definition.ResourceVersion,
                    ["previous_watermark"] = startUtc!.Value.ToString("O"),
                    ["new_watermark"] = endUtc!.Value.ToString("O")
                }
            }, cancellationToken).ConfigureAwait(false);
        }

        var succeeded = results.Count(r => r.FetchSucceeded);
        var failed = results.Count - succeeded;
        Logger.LogInformation("Load completed: {Vendor}/{Endpoint} v{Version} - {Succeeded} succeeded, {Failed} failed (run {RunId})",
            VendorName, _definition.FriendlyName, _definition.ResourceVersion, succeeded, failed, IngestionRun!.IngestionRunId);

        await EventPublisher.PublishAsync(new IngestionEvent
        {
            Type = IngestionEventTypes.RunCompleted,
            Source = eventSource,
            Subject = subject,
            Time = DateTimeOffset.UtcNow,
            Data = new Dictionary<string, object>
            {
                ["run_id"] = IngestionRun.IngestionRunId,
                ["vendor"] = VendorName,
                ["endpoint"] = _definition.FriendlyName,
                ["resource_version"] = _definition.ResourceVersion,
                ["environment"] = EnvironmentName,
                ["pages_succeeded"] = succeeded,
                ["pages_failed"] = failed,
                ["total_results"] = results.Count,
                ["duration_ms"] = (long)(DateTimeOffset.UtcNow - IngestionRun.IngestionRunStartUtc).TotalMilliseconds
            }
        }, cancellationToken).ConfigureAwait(false);

        await EventPublisher.FlushAsync(cancellationToken).ConfigureAwait(false);

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
