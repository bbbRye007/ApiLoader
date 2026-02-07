using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Engine.Adapters;
using System.Text;

namespace Canal.Ingestion.ApiLoader.Engine;

/// <summary>
/// Vendor-agnostic fetch execution engine.
///
/// Responsibilities:
///   - Execute one request (with retries)
///   - Execute a request-chain (adapter decides when a single fetch is enough via adapter's GetNextRequestAsync - this is generally to support paginated responses)
///   - Execute many seed requests concurrently (defined by maxDegreeOfParallelism parameter)
///
/// Vendor-specific concerns (auth/header rules, next-request semantics, outcome refinement)
/// live in the IVendorAdapter.
/// </summary>
public sealed class FetchEngine
{
    [SetsRequiredMembers]
    public FetchEngine(IVendorAdapter vendorAdapter, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    {
        if (vendorAdapter is null) throw new ArgumentNullException(nameof(vendorAdapter));
        if (string.IsNullOrEmpty(vendorAdapter?.HttpClient?.BaseAddress?.ToString()))
            throw new ArgumentNullException(nameof(vendorAdapter), "HttpClient.BaseAddress must be set.");

        MaxDegreeOfParallelism = maxDegreeOfParallelism;
        MaxRetries = maxRetries;
        MinRetryDelayMs = minRetryDelayMs;

        _adapter = vendorAdapter;
    }

    public required int MaxDegreeOfParallelism { get; init; }
    public required int MaxRetries { get; init; }
    public required int MinRetryDelayMs { get; init; }

    private readonly IVendorAdapter _adapter;

    

    public static string NewIdentifier => Guid.NewGuid().ToString("N");

    public async Task<List<FetchResult>> ProcessRequests(IngestionRun ingestionRun, List<Request> requests, Func<FetchResult, Task>? onPageFetched = null, CancellationToken cancellationToken = default)
    {
        var bag = new ConcurrentBag<FetchResult>();
        var options = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = MaxDegreeOfParallelism };

        await Parallel.ForEachAsync(requests, options, async (request, ct) =>
        {
            var records = await ProcessRequest(ingestionRun, request, onPageFetched, ct).ConfigureAwait(false);
            foreach (var r in records) bag.Add(r);
        }).ConfigureAwait(false);

        return bag.ToList();
    }

    public async Task<List<FetchResult>> ProcessRequest(IngestionRun ingestionRun, Request seedRequest, Func<FetchResult, Task>? onPageFetched = null, CancellationToken cancellationToken = default)
    {
        var results = new List<FetchResult>();
        FetchResult? previous = null;

        for (var stepNr = 1; ; stepNr++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var nextRequest = await _adapter.GetNextRequestAsync(seedRequest, previous, stepNr, cancellationToken).ConfigureAwait(false);
            if (nextRequest is null) break;

            var result = await PerformFetch(ingestionRun, nextRequest, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            previous = result;

            if (result.FetchSucceeded && onPageFetched is not null)
                await onPageFetched(result).ConfigureAwait(false);

            if (!result.FetchSucceeded) break;
        }

        return results;
    }

    private async Task<FetchResult> PerformFetch(IngestionRun ingestionRun, Request request, CancellationToken cancellationToken)
    {
        var result = new FetchResult(_adapter, ingestionRun, request)
        {
            RequestUri = _adapter.BuildRequestUri(request),
            PageNr = request.SequenceNr,
            PageSize = request.Pagination.RequestSize
        };

        request.RequestId = _adapter.ComputeRequestId(request);

        var maxAttempts = Math.Max(1, MaxRetries + 1);

        for (var attemptNr = 1; attemptNr <= maxAttempts; attemptNr++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            request.AttemptId = _adapter.ComputeAttemptId(request, attemptNr);
            result.AttemptNr = attemptNr;

            result.FetchOutcome = FetchStatus.NotAttempted;
            result.RequestedUtc = DateTimeOffset.UtcNow;
            result.ReceivedUtc = null;
            result.HttpStatusCode = null;
            result.Content = string.Empty;
            result.ContentType = null;
            result.ContentEncoding = null;

            Exception? caught = null;
            HttpStatusCode? statusCode = null;
            string? reasonPhrase = null;

            using var httpRequest = new HttpRequestMessage(request.HttpMethod, result.RequestUri);
            if(request.HttpMethod == HttpMethod.Post)
                httpRequest.Content = new StringContent(request.BodyParamsJson, Encoding.UTF8, "application/json");

            await _adapter.ApplyRequestHeadersAsync(httpRequest, request, cancellationToken).ConfigureAwait(false);

            // Capture the headers actually applied to the request so metadata reflects reality.
            result.EffectiveRequestHeaders = CaptureRequestHeaders(httpRequest);

            try
            {
                using var httpResponse = await _adapter.HttpClient
                    .SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                result.ReceivedUtc = DateTimeOffset.UtcNow;

                statusCode = httpResponse.StatusCode;
                reasonPhrase = httpResponse.ReasonPhrase;
                result.HttpStatusCode = statusCode;

                result.Content = await httpResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) ?? string.Empty;
                result.ContentType = httpResponse.Content.Headers.ContentType?.MediaType;
                result.ContentEncoding = httpResponse.Content.Headers.ContentEncoding is { Count: > 0 }
                    ? string.Join(",", httpResponse.Content.Headers.ContentEncoding)
                    : null;

                var generic = DetermineFetchOutcome(statusCode, ex: null);
                result.FetchOutcome = _adapter.RefineFetchOutcome(request, statusCode, result.Content, result.ContentType, generic);

                if (result.FetchOutcome == FetchStatus.Success)
                {
                    _adapter.PostProcessSuccessfulResponse(result);
                    return result;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                caught = ex;

                if (result.ReceivedUtc is null) result.ReceivedUtc = DateTimeOffset.UtcNow;

                var generic = DetermineFetchOutcome(statusCode ?? result.HttpStatusCode, caught);
                result.FetchOutcome = _adapter.RefineFetchOutcome(request, statusCode ?? result.HttpStatusCode, result.Content ?? string.Empty, result.ContentType, generic);
            }

            var requestedUtc = result.RequestedUtc ?? DateTimeOffset.UtcNow;
            var failedUtc = result.ReceivedUtc ?? DateTimeOffset.UtcNow;

            var msg = _adapter.BuildFailureMessage(statusCode ?? result.HttpStatusCode, reasonPhrase, result.FetchOutcome, result.Content ?? string.Empty, caught);
            var body = Truncate(result.Content ?? string.Empty, maxChars: 8192);

            result.Failures.Add(new FetchFailure(attemptNr, requestedUtc, failedUtc, (statusCode ?? result.HttpStatusCode) ?? HttpStatusCode.Ambiguous, msg, body));

            var isLastAttempt = attemptNr >= maxAttempts;

            if (result.FetchOutcome == FetchStatus.FailPermanent || isLastAttempt) break;
            if (result.FetchOutcome == FetchStatus.RetryImmediately) continue;

            if (result.FetchOutcome == FetchStatus.RetryTransient)
            {
                await Task.Delay(MinRetryDelayMs, cancellationToken).ConfigureAwait(false);
                continue;
            }

            break;
        }

        return result;
    }

    private static FetchStatus DetermineFetchOutcome(HttpStatusCode? code, Exception? ex = null)
    {
        if (ex is not null)
        {
            if (ex is TimeoutException) return FetchStatus.RetryTransient;
            if (ex is TaskCanceledException) return FetchStatus.RetryTransient;
            if (ex is HttpRequestException) return FetchStatus.RetryTransient;

            return FetchStatus.FailPermanent;
        }

        if (code is null) return FetchStatus.FailPermanent;

        var n = (int)code.Value;

        if (n is >= 200 and <= 299) return FetchStatus.Success;

        if (code == HttpStatusCode.Unauthorized) return FetchStatus.RetryImmediately;
        if (code == HttpStatusCode.RequestTimeout) return FetchStatus.RetryTransient;
        if (n == 429) return FetchStatus.RetryTransient;
        if (n >= 500) return FetchStatus.RetryTransient;

        return FetchStatus.FailPermanent;
    }

    private static IReadOnlyDictionary<string, string> CaptureRequestHeaders(HttpRequestMessage request)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var h in request.Headers)
        {
            if (string.IsNullOrWhiteSpace(h.Key)) continue;
            d[h.Key] = string.Join(",", h.Value ?? Array.Empty<string>());
        }

        if (request.Content is not null)
        {
            foreach (var h in request.Content.Headers)
            {
                if (string.IsNullOrWhiteSpace(h.Key)) continue;
                d[h.Key] = string.Join(",", h.Value ?? Array.Empty<string>());
            }
        }

        return d;
    }

    private static string Truncate(string s, int maxChars)
    {
        if (maxChars <= 0) return string.Empty;
        if (string.IsNullOrEmpty(s) || s.Length <= maxChars) return s;

        var trimmed = s.Substring(0, maxChars);
        return trimmed + "...<truncated>";
    }
}