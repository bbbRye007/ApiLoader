namespace Canal.Ingestion.ApiLoader.Host.TruckerCloud;

/// <summary>TruckerCloud vendor-specific settings. Bound from "TruckerCloud" config section.</summary>
public sealed class TruckerCloudSettings
{
    public string ApiUser { get; set; } = string.Empty;
    public string ApiPassword { get; set; } = string.Empty;
}
