using Canal.Ingestion.ApiLoader.Adapters.TruckerCloud.Endpoints.Internal;
using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Engine;

namespace Canal.Ingestion.ApiLoader.Adapters.TruckerCloud;

public static class TruckerCloudEndpoints
{
    // ── Simple paged endpoints (no iteration list required) ─────────────

    /// <summary>
    /// Fetches all carriers. No inputs required.
    /// Returns carrier data used as iterationList for most dependent endpoints.
    /// </summary>
    public static readonly EndpointDefinition CarriersV4 = new()
    {
        ResourceName = "carriers", FriendlyName = "Carriers", ResourceVersion = 4,
        DefaultPageSize = 1000,
        BuildRequests = RequestBuilders.Simple,
        Description = "All carriers. Iteration source for most TC endpoints."
    };

    /// <summary>
    /// Fetches all vehicles. No inputs required.
    /// Returns vehicle data used as iterationList for VehicleIgnitionV4.
    /// </summary>
    public static readonly EndpointDefinition VehiclesV4 = new()
    {
        ResourceName = "vehicles", FriendlyName = "Vehicles", ResourceVersion = 4,
        DefaultPageSize = 1000,
        BuildRequests = RequestBuilders.Simple,
        Description = "All vehicles. Iteration source for VehicleIgnitionV4."
    };

    /// <summary>
    /// Fetches all subscriptions. No inputs required.
    /// </summary>
    public static readonly EndpointDefinition SubscriptionsV4 = new()
    {
        ResourceName = "subscriptions", FriendlyName = "Subscriptions", ResourceVersion = 4,
        DefaultPageSize = 1000,
        BuildRequests = RequestBuilders.Simple,
        Description = "All subscriptions."
    };

    // ── Carrier-dependent endpoints (require iterationList from CarriersV4) ──

    /// <summary>
    /// Fetches drivers per carrier.
    /// Requires iterationList from CarriersV4.
    /// </summary>
    public static readonly EndpointDefinition DriversV4 = new()
    {
        ResourceName = "drivers", FriendlyName = "Drivers", ResourceVersion = 4,
        DefaultPageSize = 1000,
        RequiresIterationList = true,
        BuildRequests = RequestBuilders.CarrierDependent(ExtractCarrierCodes),
        Description = "Drivers per carrier.",
        DependsOn = nameof(CarriersV4)
    };

    /// <summary>
    /// Fetches risk scores per carrier.
    /// Requires iterationList from CarriersV4.
    /// </summary>
    public static readonly EndpointDefinition RiskScoresV4 = new()
    {
        ResourceName = "risk-scores", FriendlyName = "RiskScores", ResourceVersion = 4,
        DefaultPageSize = 1000,
        RequiresIterationList = true,
        BuildRequests = RequestBuilders.CarrierDependent(ExtractCarrierCodes),
        Description = "Risk scores per carrier.",
        DependsOn = nameof(CarriersV4)
    };

    /// <summary>
    /// Fetches vehicle ignition data per vehicle.
    /// Requires iterationList from VehiclesV4.
    /// WARNING: Returns extremely large payloads (900K+ lines per vehicle) with no date filtering.
    /// </summary>
    public static readonly EndpointDefinition VehicleIgnitionV4 = new()
    {
        ResourceName = "vehicles/ignition", FriendlyName = "VehicleIgnition", ResourceVersion = 4,
        DefaultPageSize = 1000,
        RequiresIterationList = true,
        BuildRequests = RequestBuilders.CarrierDependent(ExtractVehicleData),
        Description = "Vehicle ignition data. WARNING: very large payloads.",
        DependsOn = nameof(VehiclesV4)
    };

    // ── Carrier + time-window endpoints (require iterationList from CarriersV4, support watermark) ──

    /// <summary>
    /// Fetches safety events per carrier+ELD within a time window.
    /// Requires iterationList from CarriersV4. Supports watermark for incremental loads.
    /// Uses POST method. Min time span: 12 hours.
    /// </summary>
    public static readonly EndpointDefinition SafetyEventsV5 = new()
    {
        ResourceName = "safety-events", FriendlyName = "SafetyEvents", ResourceVersion = 5,
        HttpMethod = HttpMethod.Post, SupportsWatermark = true,
        DefaultPageSize = 100,
        RequiresIterationList = true,
        MinTimeSpan = TimeSpan.FromHours(12),
        BuildRequests = RequestBuilders.CarrierAndTimeWindow(ExtractCarrierCodesAndEld_ShortNames, startParamName: "startTime", endParamName: "endTime", timeFormat: "yyyy-MM-dd'T'HH:mm:ss.fff'Z'"),
        Description = "Safety events per carrier+ELD within a time window.",
        DependsOn = nameof(CarriersV4)
    };

    /// <summary>
    /// Fetches radius of operation per carrier+ELD within a time window.
    /// Requires iterationList from CarriersV4. Supports watermark for incremental loads.
    /// Min time span: 12 hours.
    /// </summary>
    public static readonly EndpointDefinition RadiusOfOperationV4 = new()
    {
        ResourceName = "radius-of-operation", FriendlyName = "RadiusOfOperation", ResourceVersion = 4,
        SupportsWatermark = true,
        DefaultPageSize = 1000,
        RequiresIterationList = true,
        MinTimeSpan = TimeSpan.FromHours(12),
        BuildRequests = RequestBuilders.CarrierAndTimeWindow(ExtractCarrierCodesAndEld_ShortNames, startParamName: "startTime", endParamName: "endTime"),
        Description = "Radius of operation within a time window.",
        DependsOn = nameof(CarriersV4)
    };

    /// <summary>
    /// Fetches GPS miles per carrier+ELD within a time window.
    /// Requires iterationList from CarriersV4. Supports watermark for incremental loads.
    /// Min time span: 12 hours.
    /// </summary>
    public static readonly EndpointDefinition GpsMilesV4 = new()
    {
        ResourceName = "enriched-data/gps-miles", FriendlyName = "GpsMiles", ResourceVersion = 4,
        SupportsWatermark = true,
        DefaultPageSize = 1000,
        RequiresIterationList = true,
        MinTimeSpan = TimeSpan.FromHours(12),
        BuildRequests = RequestBuilders.CarrierAndTimeWindow(ExtractCarrierCodesAndEld_StandardNames, startParamName: "startDateTime", endParamName: "endDateTime"),
        Description = "GPS miles within a time window.",
        DependsOn = nameof(CarriersV4)
    };

    /// <summary>
    /// Fetches zip code miles per carrier+ELD within a time window.
    /// Requires iterationList from CarriersV4. Supports watermark for incremental loads.
    /// Min time span: 12 hours.
    /// </summary>
    public static readonly EndpointDefinition ZipCodeMilesV4 = new()
    {
        ResourceName = "enriched-data/zip-code-miles", FriendlyName = "ZipCodeMiles", ResourceVersion = 4,
        SupportsWatermark = true,
        DefaultPageSize = 1000,
        RequiresIterationList = true,
        MinTimeSpan = TimeSpan.FromHours(12),
        BuildRequests = RequestBuilders.CarrierAndTimeWindow(ExtractCarrierCodesAndEld_StandardNames, startParamName: "startDateTime", endParamName: "endDateTime"),
        Description = "Zip code miles within a time window.",
        DependsOn = nameof(CarriersV4)
    };

    /// <summary>
    /// Fetches trip data per carrier+ELD within a time window.
    /// Requires iterationList from CarriersV4. Supports watermark for incremental loads.
    /// Min time span: 8 hours. Max time span: 23 hours, 59 minutes, 59 seconds.
    /// </summary>
    public static readonly EndpointDefinition TripsV5 = new()
    {
        ResourceName = "trips", FriendlyName = "Trips", ResourceVersion = 5,
        SupportsWatermark = true,
        DefaultPageSize = 1000,
        RequiresIterationList = true,
        MinTimeSpan = TimeSpan.FromHours(8),
        MaxTimeSpan = TimeSpan.FromDays(1) - TimeSpan.FromSeconds(1),
        BuildRequests = RequestBuilders.CarrierAndTimeWindow(ExtractCarrierCodesAndEld_StandardNames, startParamName: "startDateTime", endParamName: "endDateTime"),
        Description = "Trip data within a time window (max ~24h).",
        DependsOn = nameof(CarriersV4)
    };

    // ── Endpoint catalog ──────────────────────────────────────────────

    /// <summary>All TruckerCloud endpoints as CLI-ready entries.</summary>
    public static IReadOnlyList<EndpointEntry> All { get; } =
    [
        new("CarriersV4",          CarriersV4),
        new("VehiclesV4",          VehiclesV4),
        new("SubscriptionsV4",     SubscriptionsV4),
        new("DriversV4",           DriversV4),
        new("RiskScoresV4",        RiskScoresV4),
        new("VehicleIgnitionV4",   VehicleIgnitionV4),
        new("SafetyEventsV5",      SafetyEventsV5),
        new("RadiusOfOperationV4", RadiusOfOperationV4),
        new("GpsMilesV4",          GpsMilesV4),
        new("ZipCodeMilesV4",      ZipCodeMilesV4),
        new("TripsV5",             TripsV5),
    ];

    // ── Extractor helpers ───────────────────────────────────────────────

    private static List<Dictionary<string, string>> ExtractCarrierCodes(List<FetchResult> carriers)
        => List_CarrierCodes.FromList(carriers).Select(c => new Dictionary<string, string> { ["carrierCode"] = c.CarrierCode, ["codeType"] = c.CarrierCodeType }).ToList();

    private static List<Dictionary<string, string>> ExtractCarrierCodesAndEld_ShortNames(List<FetchResult> carriers)
        => List_CarrierCodesAndEldVendors.FromList(carriers).Select(c => new Dictionary<string, string> { ["carrierCode"] = c.CarrierCode, ["codeType"] = c.CarrierCodeType, ["eldVendor"] = c.EldVendor }).ToList();

    /// <summary>GpsMiles and ZipCodeMiles use different query parameter key names than other carrier+eld endpoints.</summary>
    private static List<Dictionary<string, string>> ExtractCarrierCodesAndEld_StandardNames(List<FetchResult> carriers)
        => List_CarrierCodesAndEldVendors.FromList(carriers).Select(c => new Dictionary<string, string> { ["carrierCodeValue"] = c.CarrierCode, ["carrierCodeType"] = c.CarrierCodeType, ["eldVendor"] = c.EldVendor }).ToList();

    private static List<Dictionary<string, string>> ExtractVehicleData(List<FetchResult> vehicles)
        => List_VehiclesCarrierCodesAndEldVendor.FromList(vehicles).Select(v => new Dictionary<string, string> { ["carrierCode"] = v.CarrierCode, ["codeType"] = v.CodeType, ["eldVendor"] = v.EldVendor, ["vehicleId"] = v.VehicleId, ["vehicleIdType"] = v.VehicleIdType }).ToList();
}
