namespace Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa.Endpoints;
internal class ActPendInsurAllHistoryEndpoint: Base.FmcsaEndpointBase, IEndpoint
{
    internal ActPendInsurAllHistoryEndpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    public override string ResourceName => "qh9u-swkp.json";

}

