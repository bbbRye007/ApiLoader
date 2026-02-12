namespace Canal.Ingestion.ApiLoader.Events;

/// <summary>
/// Abstracts event publishing for ingestion lifecycle events.
/// The core engine depends on this interface; concrete implementations
/// (logging, EventHubs, etc.) are wired up by the host.
/// </summary>
public interface IEventPublisher
{
    ValueTask PublishAsync(IngestionEvent @event, CancellationToken cancellationToken = default);

    /// <summary>
    /// Flushes any buffered events. Called at the end of a run to ensure delivery.
    /// </summary>
    ValueTask FlushAsync(CancellationToken cancellationToken = default);
}
