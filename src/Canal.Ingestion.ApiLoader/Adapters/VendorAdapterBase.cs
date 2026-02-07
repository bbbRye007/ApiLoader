using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Adapters;


/// <summary>
/// Vendor-agnostic base for API adapters: owns the boring-but-critical plumbing (URI building, canonical request IDs, JSON helpers),
/// and provides "exclusion lists" so token/paging noise doesn't pollute request identity.
/// </summary>
/// <remarks>
/// Derived adapters focus on vendor behavior (auth, paging, response interpretation) while the host owns transport config (HttpClient).
/// </remarks>
public abstract class VendorAdapterBase: IVendorAdapter
{
    protected VendorAdapterBase(HttpClient httpClient)
    {
        HttpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public HttpClient HttpClient { get; init ;}
    protected List<string> HeadersToExcludeFromPayloadIdentifers { get; init; } = [];
    protected List<string> QueryParamsToExcludeFromPayloadIdentifers { get; init; } = [];
    protected static string BuildQueryString(IReadOnlyDictionary<string, string> qp)
    {
        if (qp is null || qp.Count == 0) return string.Empty;

        var parts = new List<string>(qp.Count);

        foreach (var (k, v) in qp)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            if (v is null) continue;

            parts.Add($"{Uri.EscapeDataString(k)}={Uri.EscapeDataString(v)}");
        }

        return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
    }
    public abstract Uri BuildRequestUri(Request request);
    protected static bool TryParseJson(string content, out JsonDocument? json)
    {
        try
        {
            json = JsonDocument.Parse(content);
            return true;
        }
        catch (JsonException)
        {
            json = null;
            return false;
        }
    }
    protected static bool TryGetJsonElement(JsonElement element, string name, out JsonElement value)
    {
        value = default;
        return element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out value);
    }
    protected static string ComputeSha256Hex(string s) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(s ?? string.Empty))).ToLowerInvariant();
    public string ComputeRequestId(Request request) => ComputeIdHelper(request);
    public string ComputeAttemptId(Request request, int attemptNr) => ComputeIdHelper(request, attemptNr: attemptNr);
    public string ComputePageId(Request request, int pageNr) => ComputeIdHelper(request, pageNr: pageNr);
    private string ComputeIdHelper(Request request, int? attemptNr = null, int? pageNr = null)
    {
        var reqHeaders = request.RequestHeaders is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(request.RequestHeaders, StringComparer.OrdinalIgnoreCase);

        foreach(var key in HeadersToExcludeFromPayloadIdentifers)
            reqHeaders.Remove(key);

        var queryParams = request.QueryParameters is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(request.QueryParameters, StringComparer.OrdinalIgnoreCase);

        foreach(var key in QueryParamsToExcludeFromPayloadIdentifers)
            queryParams.Remove(key);

        var qpPart = string.Join("&", queryParams
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => $"{kvp.Key}={kvp.Value}"));

        var headerPart = string.Join("\n", reqHeaders
            .OrderBy(kvp => kvp.Key, StringComparer.OrdinalIgnoreCase)
            .Select(kvp => $"{kvp.Key}:{kvp.Value}"));

        var baseUrl = BaseUrl.ToString().TrimEnd('/');
        var route = request.Route.TrimStart('/');

        var canonical = new StringBuilder(512);
        canonical.Append("base_url=").Append(baseUrl).Append('\n');
        canonical.Append("vendor=").Append(request.VendorName).Append('\n');
        canonical.Append("endpoint=").Append(request.ResourceName).Append('\n');
        canonical.Append("api_version=").Append(request.ResourceVersion.ToString()).Append('\n');
        canonical.Append("route=").Append(route).Append('\n');
        canonical.Append("query=").Append(qpPart).Append('\n');
        canonical.Append("headers=").Append(headerPart).Append('\n');
        // TODO: FIGURE OUT HOW TO HANDLE SITUATION WHERE THE Query Filters are in the RequestBody instead of headers or query parameters


        if(attemptNr.HasValue) canonical.Append("attemptNr=").Append(attemptNr.ToString()).Append('\n');
        if(pageNr.HasValue) canonical.Append("pageNr=").Append(pageNr.ToString()).Append('\n');

        return ComputeSha256Hex(canonical.ToString());
    }

    #region Virtuals
    public virtual async Task ApplyRequestHeadersAsync(HttpRequestMessage httpRequest, Request request, CancellationToken cancellationToken)
    {
        httpRequest.Headers.Accept.Clear();

        foreach (var (k, v) in request.RequestHeaders)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            if (string.IsNullOrWhiteSpace(v)) continue;

            httpRequest.Headers.TryAddWithoutValidation(k, v);
        }
    }
    public virtual string ResourceNameFriendly(string resourceName)
    {
        return resourceName;
    }

    public virtual string BuildMetaDataJson(FetchResult result) => new FetchMetaData(result, redactKeys: []).JsonString();

    #endregion

    #region Abstracts
    public abstract string IngestionDomain { get; }
    public abstract string VendorName { get; }
    public abstract string BaseUrl { get; }
    public abstract bool IsExternalSource{ get; }

    public abstract void PostProcessSuccessfulResponse(FetchResult result);
    public abstract FetchStatus RefineFetchOutcome(Request request, HttpStatusCode? statusCode, string content, string? contentType, FetchStatus currentOutcome);
    public abstract string BuildFailureMessage(HttpStatusCode? statusCode, string? reasonPhrase, FetchStatus outcome, string content, Exception? ex = null);
    public abstract ValueTask<Request?> GetNextRequestAsync(Request seedRequest, FetchResult? previousResult, int stepNr, CancellationToken cancellationToken);
    #endregion
}
