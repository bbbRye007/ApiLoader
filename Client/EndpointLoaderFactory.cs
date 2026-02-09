using Azure.Storage.Blobs;
using Canal.Ingestion.ApiLoader.Engine.Adapters;
using Canal.Ingestion.ApiLoader.Model;
using Microsoft.Extensions.Logging;

namespace Canal.Ingestion.ApiLoader.Client;

public class EndpointLoaderFactory
{
    private readonly IVendorAdapter _vendorAdapter;
    private readonly BlobContainerClient _containerClient;
    private readonly string _environmentName;
    private readonly int _maxDegreeOfParallelism;
    private readonly int _maxRetries;
    private readonly int _minRetryDelayMs;
    private readonly ILoggerFactory _loggerFactory;

    public EndpointLoaderFactory(IVendorAdapter vendorAdapter, BlobContainerClient containerClient, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs, ILoggerFactory loggerFactory)
    {
        _vendorAdapter = vendorAdapter ?? throw new ArgumentNullException(nameof(vendorAdapter));
        _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        _environmentName = environmentName ?? throw new ArgumentNullException(nameof(environmentName));
        _maxDegreeOfParallelism = maxDegreeOfParallelism;
        _maxRetries = maxRetries;
        _minRetryDelayMs = minRetryDelayMs;
        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
    }

    public EndpointLoader Create(EndpointDefinition definition)
    {
        return new EndpointLoader(definition, _vendorAdapter, _containerClient, _environmentName, _maxDegreeOfParallelism, _maxRetries, _minRetryDelayMs, _loggerFactory);
    }
}
