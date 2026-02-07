using Canal.Ingestion.ApiLoader.Engine.Adapters;

namespace Canal.Ingestion.ApiLoader.Model;

public delegate List<Request> BuildRequestsDelegate(IVendorAdapter adapter, EndpointDefinition definition, PaginatedRequestSettings pagination, LoadParameters parameters);

public sealed record EndpointDefinition
{
    public required string ResourceName { get; init; }
    public required string FriendlyName { get; init; }
    public required int ResourceVersion { get; init; }
    public required BuildRequestsDelegate BuildRequests { get; init; }

    public HttpMethod HttpMethod { get; init; } = HttpMethod.Get;
    public int DefaultPageSize { get; init; } = 500;
    public int DefaultLookbackDays { get; init; } = 90;
    public int MinTimeSpanHours { get; init; } = 12;
    public bool SupportsWatermark { get; init; } = false;
}
