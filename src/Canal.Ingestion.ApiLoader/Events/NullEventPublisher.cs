namespace Canal.Ingestion.ApiLoader.Events;

/// <summary>
/// No-op publisher used when no event broker is configured.
/// Zero overhead — all methods return completed value tasks.
/// </summary>
public sealed class NullEventPublisher : IEventPublisher
{
    public static readonly NullEventPublisher Instance = new();

    private NullEventPublisher() { }

    public ValueTask PublishAsync(IngestionEvent @event, CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
