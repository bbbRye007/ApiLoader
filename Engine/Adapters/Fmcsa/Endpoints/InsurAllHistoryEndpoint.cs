namespace Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa.Endpoints;
internal class InsurAllHistoryEndpoint: Base.FmcsaEndpointBase, IEndpoint
{
    internal InsurAllHistoryEndpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    public override string ResourceName => "ypjt-5ydn.json";
}