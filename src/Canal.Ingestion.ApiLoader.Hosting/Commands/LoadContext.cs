using Canal.Ingestion.ApiLoader.Client;
using Canal.Ingestion.ApiLoader.Model;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Hosting.Commands;

/// <summary>
/// Runtime context constructed once in <see cref="VendorHostBuilder.RunAsync"/> and
/// made available to command handlers via <c>System.CommandLine.Invocation.InvocationContext</c>.
/// Owns per-invocation disposables (LoggerFactory, HttpClient, linked CancellationTokenSource).
/// </summary>
internal sealed class LoadContext : IDisposable
{
    public required EndpointLoaderFactory Factory { get; init; }
    public required IReadOnlyList<EndpointEntry> Endpoints { get; init; }
    public required ILogger Logger { get; init; }
    public required CancellationToken CancellationToken { get; init; }

    // ── Disposables owned by this context ──
    public required ILoggerFactory LoggerFactory { get; init; }
    public required HttpClient HttpClient { get; init; }
    public required CancellationTokenSource LinkedCts { get; init; }

    public void Dispose()
    {
        LinkedCts.Dispose();
        HttpClient.Dispose();
        LoggerFactory.Dispose();
    }
}
