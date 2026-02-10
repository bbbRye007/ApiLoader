using Canal.Ingestion.ApiLoader.Adapters.TruckerCloud;
using Canal.Ingestion.ApiLoader.Adapters.Fmcsa;
using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Host.Configuration;

public static class EndpointRegistry
{
    public sealed record EndpointEntry(
        string Vendor, string Name, EndpointDefinition Definition,
        string? DependsOn = null, string? Description = null);

    private static readonly List<EndpointEntry> _entries =
    [
        // TruckerCloud: simple paged
        new("truckercloud", "CarriersV4",          TruckerCloudEndpoints.CarriersV4,          Description: "All carriers. Iteration source for most TC endpoints."),
        new("truckercloud", "VehiclesV4",          TruckerCloudEndpoints.VehiclesV4,          Description: "All vehicles. Iteration source for VehicleIgnitionV4."),
        new("truckercloud", "SubscriptionsV4",     TruckerCloudEndpoints.SubscriptionsV4,     Description: "All subscriptions."),
        // TruckerCloud: carrier-dependent
        new("truckercloud", "DriversV4",           TruckerCloudEndpoints.DriversV4,           DependsOn: "CarriersV4", Description: "Drivers per carrier."),
        new("truckercloud", "RiskScoresV4",        TruckerCloudEndpoints.RiskScoresV4,        DependsOn: "CarriersV4", Description: "Risk scores per carrier."),
        new("truckercloud", "VehicleIgnitionV4",   TruckerCloudEndpoints.VehicleIgnitionV4,   DependsOn: "VehiclesV4", Description: "Vehicle ignition data. WARNING: very large payloads."),
        // TruckerCloud: carrier + time window
        new("truckercloud", "SafetyEventsV5",      TruckerCloudEndpoints.SafetyEventsV5,      DependsOn: "CarriersV4", Description: "Safety events per carrier+ELD within a time window."),
        new("truckercloud", "RadiusOfOperationV4", TruckerCloudEndpoints.RadiusOfOperationV4, DependsOn: "CarriersV4", Description: "Radius of operation within a time window."),
        new("truckercloud", "GpsMilesV4",          TruckerCloudEndpoints.GpsMilesV4,          DependsOn: "CarriersV4", Description: "GPS miles within a time window."),
        new("truckercloud", "ZipCodeMilesV4",      TruckerCloudEndpoints.ZipCodeMilesV4,      DependsOn: "CarriersV4", Description: "Zip code miles within a time window."),
        new("truckercloud", "TripsV5",             TruckerCloudEndpoints.TripsV5,             DependsOn: "CarriersV4", Description: "Trip data within a time window (max ~24h)."),
        // FMCSA: all simple paged
        new("fmcsa", "InspectionsPerUnit",              FmcsaEndpoints.InspectionsPerUnit,              Description: "Inspections per unit."),
        new("fmcsa", "InsHistAllWithHistory",            FmcsaEndpoints.InsHistAllWithHistory,            Description: "Insurance history (all with history)."),
        new("fmcsa", "ActPendInsurAllHistory",           FmcsaEndpoints.ActPendInsurAllHistory,           Description: "Active/pending insurance history."),
        new("fmcsa", "AuthHistoryAllHistory",            FmcsaEndpoints.AuthHistoryAllHistory,            Description: "Authority history."),
        new("fmcsa", "Boc3AllHistory",                   FmcsaEndpoints.Boc3AllHistory,                   Description: "BOC-3 process agent history."),
        new("fmcsa", "CarrierAllHistory",                FmcsaEndpoints.CarrierAllHistory,                Description: "Carrier registration history."),
        new("fmcsa", "CompanyCensus",                    FmcsaEndpoints.CompanyCensus,                    Description: "Company census data."),
        new("fmcsa", "CrashFile",                        FmcsaEndpoints.CrashFile,                        Description: "Crash file data."),
        new("fmcsa", "InsurAllHistory",                  FmcsaEndpoints.InsurAllHistory,                  Description: "Insurance history (all)."),
        new("fmcsa", "InspectionsAndCitations",          FmcsaEndpoints.InspectionsAndCitations,          Description: "Inspections and citations."),
        new("fmcsa", "RejectedAllHistory",               FmcsaEndpoints.RejectedAllHistory,               Description: "Rejected applications history."),
        new("fmcsa", "RevocationAllHistory",             FmcsaEndpoints.RevocationAllHistory,             Description: "Revocation history."),
        new("fmcsa", "SpecialStudies",                   FmcsaEndpoints.SpecialStudies,                   Description: "Special studies data."),
        new("fmcsa", "VehicleInspectionsAndViolations",  FmcsaEndpoints.VehicleInspectionsAndViolations,  Description: "Vehicle inspections and violations."),
        new("fmcsa", "VehicleInspectionFile",            FmcsaEndpoints.VehicleInspectionFile,            Description: "Vehicle inspection file."),
        new("fmcsa", "SmsInputMotorCarrierCensus",       FmcsaEndpoints.SmsInputMotorCarrierCensus,       Description: "SMS input motor carrier census."),
        new("fmcsa", "SmsInputInspection",               FmcsaEndpoints.SmsInputInspection,               Description: "SMS input inspection data."),
        new("fmcsa", "SmsInputViolation",                FmcsaEndpoints.SmsInputViolation,                Description: "SMS input violation data."),
        new("fmcsa", "SmsInputCrash",                    FmcsaEndpoints.SmsInputCrash,                    Description: "SMS input crash data."),
    ];

    public static IReadOnlyList<EndpointEntry> All => _entries;

    public static IReadOnlyList<string> Vendors
        => _entries.Select(e => e.Vendor).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

    public static EndpointEntry? Find(string vendor, string endpointName)
        => _entries.FirstOrDefault(e =>
            e.Vendor.Equals(vendor, StringComparison.OrdinalIgnoreCase) &&
            e.Name.Equals(endpointName, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<EndpointEntry> ForVendor(string vendor)
        => _entries.Where(e => e.Vendor.Equals(vendor, StringComparison.OrdinalIgnoreCase)).ToList();

    public static IReadOnlyList<string> EndpointNamesForVendor(string vendor)
        => ForVendor(vendor).Select(e => e.Name).ToList();

    public static List<EndpointEntry> ResolveDependencyChain(string vendor, string endpointName)
    {
        var chain = new List<EndpointEntry>();
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = endpointName;

        while (current is not null)
        {
            if (!visited.Add(current))
                throw new InvalidOperationException($"Circular dependency detected at '{current}' for vendor '{vendor}'.");
            var entry = Find(vendor, current)
                ?? throw new InvalidOperationException($"Endpoint '{current}' not found for vendor '{vendor}'.");
            chain.Add(entry);
            current = entry.DependsOn;
        }

        chain.Reverse();
        return chain;
    }
}
