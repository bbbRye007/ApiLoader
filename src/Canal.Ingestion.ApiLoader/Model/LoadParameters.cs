namespace Canal.Ingestion.ApiLoader.Model;

public sealed class LoadParameters
{
    public List<FetchResult>? IterationList { get; init; }
    public DateTimeOffset? StartUtc { get; init; }
    public DateTimeOffset? EndUtc { get; init; }
    public string BodyParamsJson { get; init; } = "{}";
}
