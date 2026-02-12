using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Events;

/// <summary>
/// Publishes events as structured JSON log entries.
/// Useful for local development and during orchestration tool evaluation
/// where you want to see events flowing without a real broker.
/// </summary>
public sealed class LoggingEventPublisher : IEventPublisher
{
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public LoggingEventPublisher(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public ValueTask PublishAsync(IngestionEvent @event, CancellationToken cancellationToken = default)
    {
        if (!_logger.IsEnabled(LogLevel.Information))
            return ValueTask.CompletedTask;

        var json = JsonSerializer.Serialize(@event, JsonOptions);
        _logger.LogInformation("[Event] {EventType} | {Json}", @event.Type, json);

        return ValueTask.CompletedTask;
    }

    public ValueTask FlushAsync(CancellationToken cancellationToken = default)
        => ValueTask.CompletedTask;
}
