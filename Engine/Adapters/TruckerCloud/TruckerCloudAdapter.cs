using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Canal.Ingestion.ApiLoader.Engine.Adapters;
using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Adapters.TruckerCloud;

/// <summary>
/// TruckerCloud-specific adapter: applies auth + headers, and owns paging logic
/// </summary>
/// <remarks>
/// - Caches auth tokens per API version with concurrency gates (not 100% sure if this is overkill but it works).
/// - Defines what gets Redacted in metadata (no secrets allowed in metadata).
/// - Extracts pagination counters from the JSON response body.
/// </remarks>
internal sealed class TruckerCloudAdapter : VendorAdapterBase, IVendorAdapter
{
    #region Identity

    private const string VendorNameConst = "TruckerCloud";
    private const string IngestionDomainConst = "Telematics";
    public const string BaseUrlConst = "https://api.truckercloud.com/api/";
    public override string IngestionDomain => IngestionDomainConst;
    public override string VendorName => VendorNameConst;
    public override string BaseUrl => HttpClient?.BaseAddress?.ToString().TrimEnd('/') ?? string.Empty;
    
    // "External" meaning: the source system is outside Canal's control.
    public override bool IsExternalSource { get; } = true;

    #endregion

    #region Configuration

    private const string AuthorizationHeaderName = "authorization";
    private const int DefaultPageSize = 1000;

    private readonly string _apiUserName;
    private readonly string _apiPassword;

    #endregion

    #region Auth Token Cache

    private readonly ConcurrentDictionary<int, string> _authTokens = new();
    private readonly ConcurrentDictionary<int, SemaphoreSlim> _authLocks = new();

    // When we see a 401, mark that API version token as invalid so the next call forces refresh.
    private readonly ConcurrentDictionary<int, bool> _credentialInvalid = new();

    private sealed record AuthRequest(string userName, string password);

    #endregion

    #region Construction

    [SetsRequiredMembers]
    public TruckerCloudAdapter(HttpClient httpClient, string apiUserName, string apiPassword)
        : base(httpClient)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiUserName, nameof(apiUserName));
        ArgumentException.ThrowIfNullOrWhiteSpace(apiPassword, nameof(apiPassword));

        HttpClient.BaseAddress = new Uri(BaseUrlConst.TrimEnd('/') + "/");
        _apiUserName = apiUserName;
        _apiPassword = apiPassword;

        // These should NOT affect the request identity (same "logical request" across pages).
        HeadersToExcludeFromPayloadIdentifers.Add(AuthorizationHeaderName);
        QueryParamsToExcludeFromPayloadIdentifers.Add("page");
        QueryParamsToExcludeFromPayloadIdentifers.Add("size");
    }

    #endregion

    #region IVendorAdapter: Request shaping

    public override async Task ApplyRequestHeadersAsync(HttpRequestMessage httpRequest, Request request, CancellationToken cancellationToken)
    {
        httpRequest.Headers.Accept.Clear();
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));


        var authValue = await GetAuthorizationHeaderAsync(request.ResourceVersion, cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(authValue))
            httpRequest.Headers.TryAddWithoutValidation("Authorization", authValue);

        foreach (var (k, v) in request.RequestHeaders)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            if (string.IsNullOrWhiteSpace(v)) continue;

            // We own Accept and Authorization.
            if (string.Equals(k, "accept", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(k, "authorization", StringComparison.OrdinalIgnoreCase)) continue;

            httpRequest.Headers.TryAddWithoutValidation(k, v);
        }
    }

    public override Uri BuildRequestUri(Request request)
    {
        var route = request.Route.TrimStart('/');

        var qs = BuildQueryString(request.QueryParameters);
        var url = $"{BaseUrl.TrimEnd('/')}/v{request.ResourceVersion}/{route}{qs}";

        return new Uri(url, UriKind.Absolute);
    }

    #endregion

    #region IVendorAdapter: Response interpretation

    public override FetchStatus RefineFetchOutcome(Request request, HttpStatusCode? statusCode, string content, string? contentType, FetchStatus currentOutcome)
    {
        // If TruckerCloud says 401, our cached token is wrong (or expired).
        if (statusCode == HttpStatusCode.Unauthorized)
        {
            _credentialInvalid[request.ResourceVersion] = true;
            return FetchStatus.RetryImmediately;
        }

        if (currentOutcome != FetchStatus.Success)
            return currentOutcome;

        var body = InspectBody(content);

        // TruckerCloud sometimes returns 200 with a timeout marker in the body.
        if (body.IndicatesVendorTimeout)
            return FetchStatus.RetryTransient;

        // Success with an empty body is suspicious; treat it as transient.
        if (body.IsEmpty)
            return FetchStatus.RetryTransient;

        // For TruckerCloud, "success" should still be parseable JSON.
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

        // If the HTTP status looks "successful" but the body indicates a vendor-side failure,
        // add a small hint so troubleshooting doesn't require a forensic lab.
        if (statusCode is not null && (int)statusCode.Value is >= 200 and <= 299)
        {
            var body = InspectBody(content);

            if (body.IndicatesVendorTimeout)
                return $"{statusPart}{reason} {outcomePart} vendor body indicates timeout".Trim();

            if (!body.IsEmpty && !body.IsValidJson)
                return $"{statusPart}{reason} {outcomePart} invalid JSON payload".Trim();
        }

        return $"{statusPart}{reason} {outcomePart}".Trim();
    }

    public override void PostProcessSuccessfulResponse(FetchResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Content))
            return;

        if (!TryParseJson(result.Content, out var json) || json is null)
            return;

        using var doc = json;
        var root = doc.RootElement;

        ApplyPaginationFromBody(root, result);

        // Defensive defaults.
        result.TotalPages = result.TotalPages <= 0 ? 1 : result.TotalPages;

        if (result.PageSize is null)
            result.PageSize = result.Request.PageSize;

        if (result.TotalElements is null)
        {
            if ((TryGetJsonElement(root, "content", out var data) || TryGetJsonElement(root, "Content", out data)) &&
                data.ValueKind == JsonValueKind.Array)
            {
                result.TotalElements = data.GetArrayLength();
            }
            else
            {
                result.TotalElements = 0;
            }
        }
    }

    private static void ApplyPaginationFromBody(JsonElement root, FetchResult result)
    {
        if (!(TryGetJsonElement(root, "Pagination", out var pagination) || TryGetJsonElement(root, "pagination", out pagination)))
            return;

        if (TryGetJsonElement(pagination, "currentPage", out var cp) && cp.ValueKind == JsonValueKind.Number)
            result.PageNr = cp.GetInt32();

        if (TryGetJsonElement(pagination, "totalPages", out var tp) && tp.ValueKind == JsonValueKind.Number)
            result.TotalPages = tp.GetInt32();

        if (TryGetJsonElement(pagination, "totalElements", out var te) && te.ValueKind == JsonValueKind.Number)
            result.TotalElements = te.GetInt32();

        if (TryGetJsonElement(pagination, "pageSize", out var ps) && ps.ValueKind == JsonValueKind.Number)
            result.PageSize = ps.GetInt32();
        
        else if (TryGetJsonElement(pagination, "size", out ps) && ps.ValueKind == JsonValueKind.Number)
            result.PageSize = ps.GetInt32();
    }

    private readonly record struct BodyInspection(bool IsEmpty, bool IndicatesVendorTimeout, bool IsValidJson);

    private static BodyInspection InspectBody(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new BodyInspection(IsEmpty: true, IndicatesVendorTimeout: false, IsValidJson: false);

        // Vendor quirk: timeouts sometimes come back as a 200 with an uppercase message.
        if (content.Contains("REQUEST TIMED OUT", StringComparison.InvariantCultureIgnoreCase))
            return new BodyInspection(IsEmpty: false, IndicatesVendorTimeout: true, IsValidJson: false);

        // "Valid JSON" check; do not keep the parsed doc.
        if (TryParseJson(content, out var json) && json is not null)
        {
            if(json.RootElement.ValueKind == JsonValueKind.Array && json.RootElement.GetArrayLength() == 0)
                return new BodyInspection(IsEmpty: true, IndicatesVendorTimeout: false, IsValidJson: true);

            json.Dispose();
            return new BodyInspection(IsEmpty: false, IndicatesVendorTimeout: false, IsValidJson: true);
        }

        return new BodyInspection(IsEmpty: false, IndicatesVendorTimeout: false, IsValidJson: false);
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

        // TruckerCloud pages are 1-based.
        if (stepNr == 1)
            return ValueTask.FromResult<Request?>(BuildPagedRequest(seedRequest, pageNr: 1));

        if (previousResult is null || !previousResult.FetchSucceeded)
            return ValueTask.FromResult<Request?>(null);

        var currentPage = previousResult.PageNr > 0 ? previousResult.PageNr : 1;
        var totalPages = previousResult.TotalPages <= 0 ? 1 : previousResult.TotalPages;

        var nextPage = currentPage + 1;
        if (nextPage > totalPages)
            return ValueTask.FromResult<Request?>(null);

        return ValueTask.FromResult<Request?>(BuildPagedRequest(seedRequest, nextPage));
    }

    private Request BuildPagedRequest(Request seedRequest, int pageNr)
    {
        var qp = new Dictionary<string, string>(seedRequest.QueryParameters, StringComparer.OrdinalIgnoreCase);

        // TruckerCloud paging contract: ?page={n}&size={n}
        qp.Remove("page");
        qp.Remove("size");

        qp["page"] = pageNr.ToString();
        qp["size"] = (seedRequest.PageSize ?? DefaultPageSize).ToString();

        var result = new Request(
            this,
            resourceName: seedRequest.ResourceName,
            resourceVersion: seedRequest.ResourceVersion,
            route: seedRequest.Route,
            queryParameters: qp,
            requestHeaders: seedRequest.RequestHeaders,
            pageSize: seedRequest.PageSize);

        // SequenceNr is used for stable blob naming / metadata ordering.
        result.SequenceNr = pageNr;

        return result;
    }

    #endregion

    #region Auth internals

    private async Task<string> GetAuthorizationHeaderAsync(int apiVersion, CancellationToken cancellationToken)
    {
        var forceRefresh = _credentialInvalid.TryGetValue(apiVersion, out var invalid) && invalid;

        if (!forceRefresh && _authTokens.TryGetValue(apiVersion, out var cached) && !string.IsNullOrWhiteSpace(cached))
            return cached;

        var gate = _authLocks.GetOrAdd(apiVersion, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            forceRefresh = _credentialInvalid.TryGetValue(apiVersion, out invalid) && invalid;

            if (!forceRefresh && _authTokens.TryGetValue(apiVersion, out cached) && !string.IsNullOrWhiteSpace(cached))
                return cached;

            // Small convenience: if we already fetched a token for another version and we're not forcing refresh,
            // reuse it. If TruckerCloud ever makes tokens version-specific, delete this block.
            if (!forceRefresh)
            {
                var tokenFromAnotherVersion = _authTokens.FirstOrDefault(kvp => !string.IsNullOrWhiteSpace(kvp.Value)).Value;
                if (!string.IsNullOrWhiteSpace(tokenFromAnotherVersion))
                {
                    _authTokens[apiVersion] = tokenFromAnotherVersion;
                    return tokenFromAnotherVersion;
                }
            }

            var newToken = await FetchAuthTokenAsync(apiVersion, cancellationToken).ConfigureAwait(false);

            _authTokens[apiVersion] = newToken ?? string.Empty;
            _credentialInvalid[apiVersion] = false;

            return newToken ?? string.Empty;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<string> FetchAuthTokenAsync(int apiVersion, CancellationToken cancellationToken)
    {
        var authUri = new Uri(HttpClient.BaseAddress!, $"v{apiVersion}/authenticate");
        var jsonToSend = $"{{ \"userName\":\"{_apiUserName}\", \"password\":\"{_apiPassword}\" }}";

        using var req = new HttpRequestMessage(HttpMethod.Post, authUri);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("*/*"));

        req.Content = new ByteArrayContent(Encoding.UTF8.GetBytes(jsonToSend));
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var resp = await HttpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
        var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("authToken", out var authKey))
            throw new Exception("Auth response missing authToken.");

        return authKey.GetString() ?? string.Empty;
    }

    #endregion

    #region Metadata

    public override string BuildMetaDataJson(FetchResult result) => new FetchMetaData(result, redactKeys: ["authorization", "userName", "password"]).JsonString();

    #endregion
}
