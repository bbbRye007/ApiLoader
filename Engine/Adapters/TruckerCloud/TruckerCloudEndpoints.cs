using Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud.Endpoints.Internal;
using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Engine;

namespace Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud;

public static class TruckerCloudEndpoints
{
    // ── Simple paged endpoints ──────────────────────────────────────────

    public static readonly EndpointDefinition Carriers = new()
    {
        ResourceName = "carriers", FriendlyName = "Carriers", ResourceVersion = 4,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition Vehicles = new()
    {
        ResourceName = "vehicles", FriendlyName = "Vehicles", ResourceVersion = 4,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition Subscriptions = new()
    {
        ResourceName = "subscriptions", FriendlyName = "Subscriptions", ResourceVersion = 4,
        BuildRequests = RequestBuilders.Simple
    };

    // ── Carrier-dependent endpoints ─────────────────────────────────────

    public static readonly EndpointDefinition Drivers = new()
    {
        ResourceName = "drivers", FriendlyName = "Drivers", ResourceVersion = 4,
        BuildRequests = RequestBuilders.CarrierDependent(ExtractCarrierCodes)
    };

    public static readonly EndpointDefinition RiskScores = new()
    {
        ResourceName = "risk-scores", FriendlyName = "RiskScores", ResourceVersion = 4,
        BuildRequests = RequestBuilders.CarrierDependent(ExtractCarrierCodes)
    };

    // VehicleIgnition is intentionally excluded from normal use.
    // When querying just 1 vehicle, the response was over a million lines long with no date filtering.
    public static readonly EndpointDefinition VehicleIgnition = new()
    {
        ResourceName = "vehicles/ignition", FriendlyName = "VehicleIgnition", ResourceVersion = 4,
        BuildRequests = RequestBuilders.CarrierDependent(ExtractVehicleData)
    };

    // ── Carrier + time-window endpoints ─────────────────────────────────

    public static readonly EndpointDefinition SafetyEvents = new()
    {
        ResourceName = "safety-events", FriendlyName = "SafetyEvents", ResourceVersion = 5,
        HttpMethod = HttpMethod.Post, SupportsWatermark = true,
        BuildRequests = RequestBuilders.CarrierAndTimeWindow(ExtractCarrierCodesAndEld, startParamName: "startTime", endParamName: "endTime", timeFormat: "yyyy-MM-dd'T'HH:mm:ss.fff'Z'")
    };

    public static readonly EndpointDefinition RadiusOfOperation = new()
    {
        ResourceName = "radius-of-operation", FriendlyName = "RadiusOfOperation", ResourceVersion = 4,
        SupportsWatermark = true,
        BuildRequests = RequestBuilders.CarrierAndTimeWindow(ExtractCarrierCodesAndEld, startParamName: "startTime", endParamName: "endTime")
    };

    public static readonly EndpointDefinition GpsMiles = new()
    {
        ResourceName = "enriched-data/gps-miles", FriendlyName = "GpsMiles", ResourceVersion = 4,
        SupportsWatermark = true,
        BuildRequests = RequestBuilders.CarrierAndTimeWindow(ExtractCarrierCodesAndEldGpsMilesKeys, startParamName: "startDateTime", endParamName: "endDateTime")
    };

    public static readonly EndpointDefinition ZipCodeMiles = new()
    {
        ResourceName = "enriched-data/zip-code-miles", FriendlyName = "ZipCodeMiles", ResourceVersion = 4,
        SupportsWatermark = true,
        BuildRequests = RequestBuilders.CarrierAndTimeWindow(ExtractCarrierCodesAndEldGpsMilesKeys, startParamName: "startDateTime", endParamName: "endDateTime")
    };

    // ── Extractor helpers ───────────────────────────────────────────────

    private static List<Dictionary<string, string>> ExtractCarrierCodes(List<FetchResult> carriers)
        => List_CarrierCodes.FromList(carriers).Select(c => new Dictionary<string, string> { ["carrierCode"] = c.CarrierCode, ["codeType"] = c.CarrierCodeType }).ToList();

    private static List<Dictionary<string, string>> ExtractCarrierCodesAndEld(List<FetchResult> carriers)
        => List_CarrierCodesAndEldVendors.FromList(carriers).Select(c => new Dictionary<string, string> { ["carrierCode"] = c.CarrierCode, ["codeType"] = c.CarrierCodeType, ["eldVendor"] = c.EldVendor }).ToList();

    /// <summary>GpsMiles and ZipCodeMiles use different query parameter key names than other carrier+eld endpoints.</summary>
    private static List<Dictionary<string, string>> ExtractCarrierCodesAndEldGpsMilesKeys(List<FetchResult> carriers)
        => List_CarrierCodesAndEldVendors.FromList(carriers).Select(c => new Dictionary<string, string> { ["carrierCodeValue"] = c.CarrierCode, ["carrierCodeType"] = c.CarrierCodeType, ["eldVendor"] = c.EldVendor }).ToList();

    private static List<Dictionary<string, string>> ExtractVehicleData(List<FetchResult> vehicles)
        => List_VehiclesCarrierCodesAndEldVendor.FromList(vehicles).Select(v => new Dictionary<string, string> { ["carrierCode"] = v.CarrierCode, ["codeType"] = v.CodeType, ["eldVendor"] = v.EldVendor, ["vehicleId"] = v.VehicleId, ["vehicleIdType"] = v.VehicleIdType }).ToList();
}
