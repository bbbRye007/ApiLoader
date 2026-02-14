using Canal.Ingestion.ApiLoader.Client;
using Canal.Ingestion.ApiLoader.Model;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Hosting.Commands;

/// <summary>
/// Runtime context constructed once in <see cref="VendorHostBuilder.RunAsync"/> and
/// made available to command handlers via <c>System.CommandLine.Invocation.InvocationContext</c>.
/// </summary>
internal sealed class LoadContext
{
    public required EndpointLoaderFactory Factory { get; init; }
    public required IReadOnlyList<EndpointEntry> Endpoints { get; init; }
    public required ILogger Logger { get; init; }
    public required CancellationToken CancellationToken { get; init; }
}
