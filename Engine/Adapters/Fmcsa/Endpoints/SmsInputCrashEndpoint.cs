namespace Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa.Endpoints;
internal class SmsInputCrashEndpoint: Base.FmcsaEndpointBase, IEndpoint
{
    internal SmsInputCrashEndpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    public override string ResourceName => "4wxs-vbns.json";
}