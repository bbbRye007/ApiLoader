using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Canal.Ingestion.ApiLoader.Model;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Adapters.Fmcsa;

/// <summary>
/// FMCSA-specific adapter: owns paging logic.
/// </summary>
/// <remarks>
/// FMCSA uses Socrata-style paging:
///  - $limit (page size)
///  - $offset (0-based row offset)
///
/// The API does NOT return pagination counters, so we keep requesting until the body is an empty JSON payload
/// (typically an empty array) or empty/whitespace.
/// </remarks>
public sealed class FmcsaAdapter : VendorAdapterBase, IVendorAdapter
{
    #region Identity
    private const string VendorNameConst = "Fmcsa";
    private const string IngestionDomainConst = "CarrierInfo";
    private const string BaseUrlConst = "https://data.transportation.gov/resource/";
    public override string IngestionDomain => IngestionDomainConst;
    public override string VendorName => VendorNameConst;
    public override string BaseUrl => HttpClient?.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
    
    // "External" meaning: the source system is outside Canal's control.
    public override bool IsExternalSource { get; } = true;
    #endregion
    #region Configuration
    private const int DefaultRequestSize = 500;
    private readonly ILogger<FmcsaAdapter> _logger;
    #endregion
    #region Construction
    [SetsRequiredMembers]
    public FmcsaAdapter(HttpClient httpClient, ILogger<FmcsaAdapter> logger, IReadOnlyList<EndpointEntry>? endpoints = null)
      : base(httpClient)
    {
        _logger = logger;
        _friendlyNamesByResource = (endpoints ?? FmcsaEndpoints.All).ToFrozenDictionary(
            e => e.Definition.ResourceName,
            e => e.Definition.FriendlyName,
            StringComparer.OrdinalIgnoreCase);
        // These should NOT affect the request identity (same "logical request" across pages).
        HttpClient.BaseAddress = new Uri(BaseUrlConst.TrimEnd('/'));
        QueryParamsToExcludeFromPayloadIdentifers.Add("$limit");
        QueryParamsToExcludeFromPayloadIdentifers.Add("$offset");
    }
    #endregion
    #region IVendorAdapter: Request shaping
    public override Task ApplyRequestHeadersAsync(HttpRequestMessage httpRequest, Request request, CancellationToken cancellationToken)
    {
        // FMCSA does not require auth.
        // Default to */* unless the caller wants something else.
        httpRequest.Headers.Accept.Clear();
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*")); // could change to "applicationdata/json" instead of "anything goes".. 
        foreach (var (k, v) in request.RequestHeaders)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            if (string.IsNullOrWhiteSpace(v)) continue;
            // We own Accept.
            if (string.Equals(k, "accept", StringComparison.OrdinalIgnoreCase)) continue;
            httpRequest.Headers.TryAddWithoutValidation(k, v);
        }
        return Task.CompletedTask;
    }

    public override Uri BuildRequestUri(Request request)
    {
        var route = request.Route.TrimStart('/');

        var qs = BuildQueryString(request.QueryParameters);
        var url = $"{BaseUrl.TrimEnd('/')}/{route}{qs}";

        return new Uri(url, UriKind.Absolute);
    }
        
    #endregion
    #region IVendorAdapter: Response interpretation
    public override FetchStatus RefineFetchOutcome(Request request, HttpStatusCode? statusCode, string content, string? contentType, FetchStatus currentOutcome)
    {
        // No auth token logic. If we see 401, treat it as permanent failure.
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("Received 401 for FMCSA {Endpoint}, treating as permanent failure (no auth mechanism)", request.ResourceName);
            return FetchStatus.FailPermanent;
        }
        if (currentOutcome != FetchStatus.Success)
            return currentOutcome;
        var body = InspectBody(content);
        // A "success" response should still be valid JSON.
        if (!body.IsValidJson)
            return FetchStatus.FailPermanent;
        return FetchStatus.Success;
    }
    public override string BuildFailureMessage(HttpStatusCode? statusCode, string? reasonPhrase, FetchStatus outcome, string content, Exception? ex = null)
    {
        var statusPart = statusCode is null ? "HTTP <null>" : $"HTTP {(int)statusCode.Value} {statusCode.Value}";
        var reason = string.IsNullOrWhiteSpace(reasonPhrase) ? string.Empty : $" {reasonPhrase.Trim()}";
        var outcomePart = $"({outcome})";
        if (ex is not null)
        {
            var exPart = $"{ex.GetType().Name}: {ex.Message}";
            return $"{statusPart}{reason} {outcomePart} {exPart}".Trim();
        }
        if (statusCode is not null && (int)statusCode.Value is >= 200 and <= 299)
        {
            var body = InspectBody(content);
            if (!body.IsEmpty && !body.IsValidJson)
                return $"{statusPart}{reason} {outcomePart} invalid JSON payload".Trim();
        }
        return $"{statusPart}{reason} {outcomePart}".Trim();
    }
    public override void PostProcessSuccessfulResponse(FetchResult result)
    {
        // Derive page-ish info from the request ($limit/$offset), not the body.
        ApplyPaginationFromRequest(result);
        if (string.IsNullOrWhiteSpace(result.Content))
            return;
        if (!TryParseJson(result.Content, out var json) || json is null)
            return;
        using var doc = json;
        var root = doc.RootElement;
        // Defensive defaults.
        result.TotalPages = 1;
        // FMCSA payloads are typically arrays.
        if (result.TotalElements is null)
        {
            if (root.ValueKind == JsonValueKind.Array)
                result.TotalElements = root.GetArrayLength();
            else
                result.TotalElements = 0;
        }
    }
    private static void ApplyPaginationFromRequest(FetchResult result)
    {
        // Defaults
        var limit = result.Request.PageSize ?? DefaultRequestSize;
        var offset = 0;
        if (result.Request.QueryParameters.TryGetValue("$limit", out var limitText) && int.TryParse(limitText, out var parsedLimit) && parsedLimit > 0)
            limit = parsedLimit;
        if (result.Request.QueryParameters.TryGetValue("$offset", out var offsetText) && int.TryParse(offsetText, out var parsedOffset) && parsedOffset >= 0)
            offset = parsedOffset;
        result.PageSize = limit;
        // Normalize to a 1-based sequence/page number for metadata/blob naming.
        // Example: offset 0..limit-1 => PageNr 1, offset limit..2*limit-1 => PageNr 2, etc.
        result.PageNr = (offset / Math.Max(1, limit)) + 1;
        // TotalPages/TotalElements are unknown from the API.
        result.TotalPages = 1;
    }
    private readonly record struct BodyInspection(bool IsEmpty, bool IsValidJson);
    private static BodyInspection InspectBody(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new BodyInspection(IsEmpty: true, IsValidJson: false);
        if (TryParseJson(content, out var json) && json is not null)
        {
            var isEmpty = json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() == 0;
            json.Dispose();
            return new BodyInspection(IsEmpty: isEmpty, IsValidJson: true);
        }
        return new BodyInspection(IsEmpty: false, IsValidJson: false);
    }
    #endregion
    #region IVendorAdapter: Paging / request sequencing
    public override ValueTask<Request?> GetNextRequestAsync(Request seedRequest, FetchResult? previousResult, int stepNr, CancellationToken cancellationToken)
    {
        if (stepNr <= 0)
            throw new ArgumentOutOfRangeException(nameof(stepNr), "stepNr must be 1-based.");

        // Non-paged endpoints: one-and-done.
        if (!seedRequest.PageSize.HasValue)
        {
            if (stepNr == 1)
            {
                if (seedRequest.SequenceNr <= 0)
                    seedRequest.SequenceNr = 1;
                return ValueTask.FromResult<Request?>(seedRequest);
            }
            return ValueTask.FromResult<Request?>(null);
        }

        var limit = seedRequest.PageSize ?? DefaultRequestSize;

        // FMCSA offsets are 0-based.
        if (stepNr == 1)
            return ValueTask.FromResult<Request?>(BuildPagedRequest(seedRequest, offset: 0, limit: limit));

        if (previousResult is null || !previousResult.FetchSucceeded)
            return ValueTask.FromResult<Request?>(null);

        // Keep fetching until the API returns an "empty" JSON payload.
        var body = InspectBody(previousResult.Content);
        if (body.IsEmpty)
            return ValueTask.FromResult<Request?>(null);

        var previousOffset = TryGetOffset(previousResult.Request, defaultValue: 0);
        var previousLimit = TryGetLimit(previousResult.Request, defaultValue: limit);
        var nextOffset = checked(previousOffset + previousLimit);

        return ValueTask.FromResult<Request?>(BuildPagedRequest(seedRequest, offset: nextOffset, limit: previousLimit));
    }

    private static int TryGetOffset(Request req, int defaultValue)
      => req.QueryParameters.TryGetValue("$offset", out var s) && int.TryParse(s, out var v) && v >= 0 ? v : defaultValue;

    private static int TryGetLimit(Request req, int defaultValue)
      => req.QueryParameters.TryGetValue("$limit", out var s) && int.TryParse(s, out var v) && v > 0 ? v : defaultValue;

    private Request BuildPagedRequest(Request seedRequest, int offset, int limit)
    {
        var qp = new Dictionary<string, string>(seedRequest.QueryParameters, StringComparer.OrdinalIgnoreCase);

        // FMCSA paging contract: ?$limit={n}&$offset={n}
        qp.Remove("$limit");
        qp.Remove("$offset");
        qp["$offset"] = offset.ToString();
        qp["$limit"] = limit.ToString();

        var result = new Request(
          this,
          resourceName: seedRequest.ResourceName,
          resourceVersion: seedRequest.ResourceVersion,
          route: seedRequest.Route,
          queryParameters: qp,
          requestHeaders: seedRequest.RequestHeaders,
          pageSize: seedRequest.PageSize,
          httpMethod: seedRequest.HttpMethod,
          bodyParamsJson: seedRequest.BodyParamsJson);

        // Stable blob naming / ordering:
        // turn offset+limit into a human 1-based page sequence.
        result.SequenceNr = (offset / Math.Max(1, limit)) + 1;

        return result;
    }
    #endregion
    #region Metadata

    /// <summary>
    /// O(1) lookup cache: resource name → friendly name, built once from the endpoint catalog
    /// provided at construction time.
    /// </summary>
    private readonly FrozenDictionary<string, string> _friendlyNamesByResource;

    public override string ResourceNameFriendly(string resourceName)
    {
        return _friendlyNamesByResource.TryGetValue(resourceName, out var friendly)
            ? friendly
            : resourceName;
    }
    #endregion
}
