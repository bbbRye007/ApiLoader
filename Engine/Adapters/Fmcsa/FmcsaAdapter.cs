using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Canal.Ingestion.ApiLoader.Engine.Adapters;
using Canal.Ingestion.ApiLoader.Model;

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
internal sealed class FmcsaAdapter : VendorAdapterBase, IVendorAdapter
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
    #endregion
    #region Construction
    [SetsRequiredMembers]
    public FmcsaAdapter(HttpClient httpClient)
      : base(httpClient)
    {
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
            return FetchStatus.FailPermanent;
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
        var limit = result.Request.Pagination.RequestSize ?? DefaultRequestSize;
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
            if (json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() == 0)
                return new BodyInspection(IsEmpty: true, IsValidJson: true);
            json.Dispose();
            return new BodyInspection(IsEmpty: false, IsValidJson: true);
        }
        return new BodyInspection(IsEmpty: false, IsValidJson: false);
    }
    #endregion
    #region IVendorAdapter: Paging / request sequencing
    public override ValueTask<Request?> GetNextRequestAsync(Request seedRequest, FetchResult? previousResult, int stepNr, CancellationToken cancellationToken)
    {
        if (stepNr <= 0)
            throw new ArgumentOutOfRangeException(nameof(stepNr), "stepNr must be 1-based.");
        // Only inject paging params when the caller explicitly enabled paging.
        var pagingEnabled =
          seedRequest.Pagination.RequestSize.HasValue ||
          seedRequest.Pagination.NrRequestsAllowedBeforeAbort.HasValue;
          
        // Non-paged endpoints: one-and-done.
        if (!pagingEnabled)
        {
            if (stepNr == 1)
            {
                if (seedRequest.SequenceNr <= 0)
                    seedRequest.SequenceNr = 1;
                return ValueTask.FromResult<Request?>(seedRequest);
            }
            return ValueTask.FromResult<Request?>(null);
        }


        // Vendor-neutral safety cap: max number of requests to fetch (COUNT).
        if (seedRequest.Pagination.NrRequestsAllowedBeforeAbort.HasValue && stepNr > seedRequest.Pagination.NrRequestsAllowedBeforeAbort.Value)
            return ValueTask.FromResult<Request?>(null);
        var limit = seedRequest.Pagination.RequestSize ?? DefaultRequestSize;
        // StartIndex is vendor-neutral; for offset-based vendors we interpret it as a 1-based "page start".
        // Default StartIndex=1 => offset 0.
        var startOffset = Math.Max(0, (seedRequest.Pagination.StartIndex - 1) * limit);
        if (stepNr == 1)
            return ValueTask.FromResult<Request?>(BuildPagedRequest(seedRequest, offset: startOffset, limit: limit));
        if (previousResult is null || !previousResult.FetchSucceeded)
            return ValueTask.FromResult<Request?>(null);
        // Keep fetching until the API returns an "empty" JSON payload.
        var body = InspectBody(previousResult.Content);
        if (body.IsEmpty)
            return ValueTask.FromResult<Request?>(null);
        var previousOffset = TryGetOffset(previousResult.Request, defaultValue: startOffset);
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
          pagination: seedRequest.Pagination);
        // Stable blob naming / ordering:
        // turn offset+limit into a human 1-based page sequence.
        result.SequenceNr = (offset / Math.Max(1, limit)) + 1;
        return result;
    }
    #endregion
    #region Metadata
    
    private readonly ReadOnlyDictionary<string,string> _endpointDescriptions = new(new Dictionary<string, string> (StringComparer.OrdinalIgnoreCase)
    {
        {"qh9u-swkp.json", "ActPendInsurAllHistory"},
        {"9mw4-x3tu.json", "AuthHistoryAllHistory"},
        {"2emp-mxtb.json", "Boc3AllHistory"},
        {"6eyk-hxee.json", "CarrierAllHistory"},
        {"az4n-8mr2.json", "CompanyCensus"},
        {"aayw-vxb3.json", "CrashFile"},
        {"6sqe-dvqs.json", "InsHistAllWithHistory"},
        {"qbt8-7vic.json", "InspectionsAndCitations"},
        {"wt8s-2hbx.json", "InspectionsPerUnit"},
        {"ypjt-5ydn.json", "InsurAllHistory"},
        {"96tg-4mhf.json", "RejectedAllHistory"},
        {"sa6p-acbp.json", "RevocationAllHistory"},
        {"4wxs-vbns.json", "SmsInputCrash"},
        {"rbkj-cgst.json", "SmsInputInspection"},
        {"kjg3-diqy.json", "SmsInputMotorCarrierCensus"},
        {"8mt8-2mdr.json", "SmsInputViolation"},
        {"5qik-smay.json", "SpecialStudies"},
        {"fx4q-ay7w.json", "VehicleInspectionFile"},
        {"876r-jsdb.json", "VehicleInspectionsAndViolations"},
    });
    
    public override string ResourceNameFriendly(string resourceName)
    {
        if(_endpointDescriptions.ContainsKey(resourceName))
        {
            return _endpointDescriptions[resourceName];
        }
        else
        {
            return resourceName;
        }
    }
    #endregion
}