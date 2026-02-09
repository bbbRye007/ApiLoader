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
    public bool SupportsWatermark { get; init; } = false;
    public bool RequiresIterationList { get; init; } = false;
}
