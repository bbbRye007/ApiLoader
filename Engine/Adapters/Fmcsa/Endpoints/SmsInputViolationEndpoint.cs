namespace Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa.Endpoints;
internal class SmsInputViolationEndpoint: Base.FmcsaEndpointBase, IEndpoint
{
    internal SmsInputViolationEndpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    public override string ResourceName => "8mt8-2mdr.json";
}

