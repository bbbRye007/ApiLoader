namespace Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa.Endpoints;
internal class SmsInputMotorCarrierCensusEndpoint: Base.FmcsaEndpointBase, IEndpoint
{
    internal SmsInputMotorCarrierCensusEndpoint(IVendorAdapter vendorAdapter, string environmentName, int maxDegreeOfParallelism, int maxRetries, int minRetryDelayMs)
    : base(vendorAdapter, environmentName, maxDegreeOfParallelism, maxRetries, minRetryDelayMs) {}

    public override string ResourceName => "kjg3-diqy.json";
}