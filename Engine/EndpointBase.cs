using Canal.Ingestion.ApiLoader.Engine.Adapters;
using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Engine;

public abstract class EndpointBase: IEndpoint
{
    public EndpointBase(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    {
        _environmentName = environmentName ?? throw new ArgumentNullException(nameof(environmentName));

        _vendorAdapter = vendorAdapter ?? throw new ArgumentNullException(nameof(vendorAdapter));

        _fetcher = new FetchEngine(_vendorAdapter, maxDegreeOfParallelism, maxRetries, minRetryDelayMs);
    }

    protected readonly string _environmentName;
    protected readonly IVendorAdapter _vendorAdapter;
    protected readonly FetchEngine _fetcher;

    public abstract string ResourceName { get; }
    public virtual int ResourceVersion {get;} = 1; // for vendors without versioned endpoints, just don't override this property in the endpoint classes for that vendor.
}

