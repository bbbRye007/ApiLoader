using Canal.Ingestion.ApiLoader.Adapters;

namespace Canal.Ingestion.ApiLoader.Model;

public delegate List<Request> BuildRequestsDelegate(IVendorAdapter adapter, EndpointDefinition definition, int? pageSize, LoadParameters parameters);

public sealed record EndpointDefinition
{
    public required string ResourceName { get; init; }
    public required string FriendlyName { get; init; }
    public required int ResourceVersion { get; init; }
    public required BuildRequestsDelegate BuildRequests { get; init; }

    public HttpMethod HttpMethod { get; init; } = HttpMethod.Get;
    public int? DefaultPageSize { get; init; }
    public int DefaultLookbackDays { get; init; } = 90;
    public TimeSpan? MinTimeSpan { get; init; }
    public TimeSpan? MaxTimeSpan { get; init; }

    /// <summary>
    /// Whether this endpoint supports incremental (watermark-based) loads.
    /// When <c>true</c>, <see cref="MinTimeSpan"/> and <see cref="MaxTimeSpan"/> constrain the
    /// time window, and CLI options <c>--start-utc</c> / <c>--end-utc</c> / <c>--no-save-watermark</c>
    /// are exposed. Typically combined with <see cref="RequiresIterationList"/> and <see cref="DependsOn"/>.
    /// </summary>
    public bool SupportsWatermark { get; init; } = false;

    /// <summary>
    /// Whether this endpoint requires an iteration list produced by a dependency endpoint.
    /// When <c>true</c>, <see cref="DependsOn"/> should name the provider endpoint.
    /// </summary>
    public bool RequiresIterationList { get; init; } = false;

    /// <summary>Human-readable description shown in CLI help text. Null means no description.</summary>
    public string? Description { get; init; }

    /// <summary>
    /// CLI name of the endpoint that must be fetched first to produce an iteration list for this endpoint.
    /// Null means this endpoint is independent (simple paged). Only linear single-parent chains are supported;
    /// each endpoint may depend on at most one other endpoint.
    /// </summary>
    public string? DependsOn { get; init; }
}
