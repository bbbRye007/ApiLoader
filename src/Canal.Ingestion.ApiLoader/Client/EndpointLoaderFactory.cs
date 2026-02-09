using Canal.Ingestion.ApiLoader.Adapters;
using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Storage;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Client;

public class EndpointLoaderFactory
{
    private readonly IVendorAdapter _vendorAdapter;
    private readonly IIngestionStore _store;
    private readonly string _environmentName;
    private readonly int _maxDegreeOfParallelism;
    private readonly int _maxRetries;
    private readonly int _minRetryDelayMs;
    private readonly ILoggerFactory _loggerFactory;

    public EndpointLoaderFactory(IVendorAdapter vendorAdapter, IIngestionStore store, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs, ILoggerFactory loggerFactory)
    {
        _vendorAdapter = vendorAdapter ?? throw new ArgumentNullException(nameof(vendorAdapter));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _environmentName = environmentName ?? throw new ArgumentNullException(nameof(environmentName));
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _maxRetries = maxRetries;
        _minRetryDelayMs = minRetryDelayMs;
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public EndpointLoader Create(EndpointDefinition definition)
    {
        return new EndpointLoader(definition, _vendorAdapter, _store, _environmentName, _maxDegreeOfParallelism, _maxRetries, _minRetryDelayMs, _loggerFactory);
    }
}
