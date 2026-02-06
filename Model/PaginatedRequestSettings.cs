namespace Canal.Ingestion.ApiLoader.Model;

/// <summary>
/// Vendor-neutral(ish) pagination controls.
/// 
/// TO BE TRULEY VENDOR-NEURALY: If an endpoint doesn't use this kind of pagination, but maybe uses cursor-based apis (nextToken, etc), this will need to be re-engineered to keep it truly vendor-agnostic.
/// TO BE TRULEY VENDOR-NEURALY: For now - this needs to be delivered for the first Vendor, TruckerCloud, so trying to anticipate every possible pagination scheme on earth is overkill 
/// 
/// Hopefully it won't be too difficult to figure out how to generalize pagnation handling if/when the time comes.
/// This expresses the caller's intent; the vendor adapter decides how to translate it
/// into query parameters, headers, cursors, etc.
/// </summary>
public sealed record PaginatedRequestSettings
{
    public static PaginatedRequestSettings None { get; } = new();

    /// <summary>
    /// The first page to request when an API uses page numbers. Defaults to 1.
    /// Vendors that use 0-based pages can interpret this however they like.
    /// </summary>
    public int StartIndex { get; init; } = 1;

    /// <summary>
    /// Preferred page size (row limit) if the vendor supports it.
    /// </summary>
    public int? RequestSize { get; init; }

    /// <summary>
    /// Maximum number of pages to fetch for this logical request (a safety cap).
    /// This is a COUNT cap (ex: 3 means "fetch at most 3 pages"), not "stop at page #3".
    /// </summary>
    public int? NrRequestsAllowedBeforeAbort { get; set; }

    public PaginatedRequestSettings() { }

    public PaginatedRequestSettings(int? startIndex = 1, int? requestSize = null, int? nrRequestsAllowedBeforeAbort = null)
    {
        if (startIndex.HasValue && startIndex.Value < 0) throw new ArgumentOutOfRangeException(nameof(startIndex), " must be >= 0.");
        if (requestSize.HasValue && requestSize.Value <= 0) throw new ArgumentOutOfRangeException(nameof(requestSize), " must be > 0.");
        if (nrRequestsAllowedBeforeAbort.HasValue && nrRequestsAllowedBeforeAbort.Value <= 0) throw new ArgumentOutOfRangeException(nameof(nrRequestsAllowedBeforeAbort), " must be > 0.");

        if(startIndex.HasValue) StartIndex = startIndex.Value;
        RequestSize = requestSize;
        NrRequestsAllowedBeforeAbort = nrRequestsAllowedBeforeAbort;
    }
}