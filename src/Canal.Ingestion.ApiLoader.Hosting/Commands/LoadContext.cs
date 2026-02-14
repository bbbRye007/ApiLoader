using Canal.Ingestion.ApiLoader.Client;
using Canal.Ingestion.ApiLoader.Model;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Hosting.Commands;

/// <summary>
/// Runtime context constructed once in <see cref="VendorHostBuilder.RunAsync"/> and
/// made available to command handlers via <c>System.CommandLine.Invocation.InvocationContext</c>.
/// Owns per-invocation disposables (LoggerFactory, HttpClient, linked and process
/// CancellationTokenSources) and unregisters process-lifetime event handlers on dispose.
/// </summary>
internal sealed class LoadContext : IDisposable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _linkedCts;
    private readonly CancellationTokenSource _processCts;
    private readonly Action _cleanupEventHandlers;
    /// <summary>
    /// Defensive: neither IVendorAdapter nor IIngestionStore is IDisposable today,
    /// but stored for disposal in case future implementations hold resources.
    /// </summary>
    private readonly object? _vendorAdapter;
    private readonly object? _ingestionStore;
    private bool _disposed;

    public LoadContext(
        ILoggerFactory loggerFactory,
        HttpClient httpClient,
        CancellationTokenSource linkedCts,
        CancellationTokenSource processCts,
        Action cleanupEventHandlers,
        object? vendorAdapter = null,
        object? ingestionStore = null)
    {
        _loggerFactory = loggerFactory;
        _httpClient = httpClient;
        _linkedCts = linkedCts;
        _processCts = processCts;
        _cleanupEventHandlers = cleanupEventHandlers;
        _vendorAdapter = vendorAdapter;
        _ingestionStore = ingestionStore;
    }

    public required EndpointLoaderFactory Factory { get; init; }
    public required IReadOnlyList<EndpointEntry> Endpoints { get; init; }
    public required ILogger Logger { get; init; }
    public required CancellationToken CancellationToken { get; init; }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        try
        {
            _cleanupEventHandlers();
        }
        finally
        {
            try { (_vendorAdapter as IDisposable)?.Dispose(); }
            finally
            {
                try { (_ingestionStore as IDisposable)?.Dispose(); }
                finally
                {
                    try { _linkedCts.Dispose(); }
                    finally
                    {
                        try { _processCts.Dispose(); }
                        finally
                        {
                            try { _httpClient.Dispose(); }
                            finally { _loggerFactory.Dispose(); }
                        }
                    }
                }
            }
        }
    }
}
