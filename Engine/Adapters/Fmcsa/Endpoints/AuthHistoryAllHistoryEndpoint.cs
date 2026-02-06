namespace Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa.Endpoints;
internal class AuthHistoryAllHistoryEndpoint: Base.FmcsaEndpointBase, IEndpoint
{
    internal AuthHistoryAllHistoryEndpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    public override string ResourceName => "9mw4-x3tu.json";
}


