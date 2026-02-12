namespace Canal.Ingestion.ApiLoader.Events;

/// <summary>
/// A lifecycle event emitted during ingestion.
/// Type follows reverse-DNS convention aligned with CloudEvents spec:
///   canal.ingestion.apiloader.{category}.{action}
/// </summary>
public sealed record IngestionEvent
{
    public required string Type { get; init; }
    public required string Source { get; init; }
    public required string Subject { get; init; }
    public required DateTimeOffset Time { get; init; }
    public required IReadOnlyDictionary<string, object> Data { get; init; }
}

/// <summary>
/// Constants for well-known event types emitted by the ingestion engine.
/// </summary>
public static class IngestionEventTypes
{
    private const string Prefix = "canal.ingestion.apiloader";

    public const string RunStarted = $"{Prefix}.run.started";
    public const string RunCompleted = $"{Prefix}.run.completed";
    public const string RunFailed = $"{Prefix}.run.failed";
    public const string WatermarkAdvanced = $"{Prefix}.endpoint.watermark.advanced";
}
