using Canal.Ingestion.ApiLoader.Adapters;

namespace Canal.Ingestion.ApiLoader.Model;

public sealed class Request
{
    public Request(IVendorAdapter vendorAdapter, string resourceName, int resourceVersion, string? route = null,
                   IReadOnlyDictionary<string, string>? queryParameters = null, IReadOnlyDictionary<string, string>? requestHeaders = null,
                   PaginatedRequestSettings? pagination = null, HttpMethod? httpMethod = null, string bodyParamsJson = "{}")
    {
        string vendorAdapter_baseUrl = vendorAdapter.BaseUrl;
        ArgumentNullException.ThrowIfNull(vendorAdapter, nameof(vendorAdapter));
        ArgumentException.ThrowIfNullOrEmpty(vendorAdapter_baseUrl, nameof(vendorAdapter_baseUrl));
        ArgumentException.ThrowIfNullOrEmpty(resourceName, nameof(resourceName));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resourceVersion, nameof(resourceVersion));

        if(httpMethod != null)
            HttpMethod = httpMethod;

        BodyParamsJson = bodyParamsJson;

        _vendorAdapter = vendorAdapter;
        ResourceName = resourceName;
        ResourceVersion = resourceVersion;

        Route = route ?? resourceName;

        Pagination = pagination ?? PaginatedRequestSettings.None;

        QueryParameters = queryParameters is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(queryParameters, StringComparer.OrdinalIgnoreCase);

        RequestHeaders = requestHeaders is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(requestHeaders, StringComparer.OrdinalIgnoreCase);
    }
    private readonly IVendorAdapter _vendorAdapter;

    public string VendorName => _vendorAdapter.VendorName;

    public string ResourceName { get; }
    public string ResourceNameFriendly => _vendorAdapter.ResourceNameFriendly(ResourceName);
    public int ResourceVersion { get; }
    public string Route { get; }
    public HttpMethod HttpMethod {get;} = HttpMethod.Get;

    public string BodyParamsJson {get;}

    /// <summary>
    /// Vendor-owned request sequencing marker.
    /// For page-based APIs, adapters should set this to the page number.
    /// For cursor/continuation APIs, adapters can set this to the 1-based step number.
    /// </summary>
    public int SequenceNr { get; set; } = 1;

    public PaginatedRequestSettings Pagination { get; }

    public string RequestId { get; set; } = string.Empty;
    public string AttemptId { get; set; } = string.Empty;

    public IReadOnlyDictionary<string, string> QueryParameters { get; }
    public IReadOnlyDictionary<string, string> RequestHeaders { get; }
}
