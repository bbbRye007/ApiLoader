using System.Security.Cryptography;

namespace Canal.Ingestion.ApiLoader.Model;
public sealed class IngestionRun 
{
    public IngestionRun(string environmentName, string ingestionDomain, string vendorName)
    {

        IngestionRunStartUtc = DateTimeOffset.UtcNow;
        _epochsuffix = RandomNumberGenerator.GetInt32(0,10000).ToString("D4");

        EnvironmentName = environmentName;
        IngestionDomain = ingestionDomain;
        VendorName = vendorName;
    }
    public DateTimeOffset IngestionRunStartUtc { get; private set; }
    private string _epochsuffix {get; set; }
    public string IngestionRunId => IngestionRunStartUtc.ToUnixTimeMilliseconds().ToString() + _epochsuffix ; 
    public string EnvironmentName { get; init; }
    public string IngestionDomain { get; init; }
    public string VendorName { get; init; }
}
