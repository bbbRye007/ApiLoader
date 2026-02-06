namespace Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa.Endpoints;
internal class InsHistAllWithHistoryEndpoint: Base.FmcsaEndpointBase, IEndpoint
{
    internal InsHistAllWithHistoryEndpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    public override string ResourceName => "6sqe-dvqs.json";
}
