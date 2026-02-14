using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Engine;

namespace Canal.Ingestion.ApiLoader.Adapters.Fmcsa;

/// <summary>
/// FMCSA (Federal Motor Carrier Safety Administration) public data endpoints.
/// All endpoints are simple paged (Socrata API). No iteration list or time window required.
/// </summary>
public static class FmcsaEndpoints
{
    /// <summary>Active/Pending insurance history.</summary>
    public static readonly EndpointDefinition ActPendInsurAllHistory = new()
    {
        ResourceName = "qh9u-swkp.json", FriendlyName = "ActPendInsurAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Active/pending insurance history."
    };

    /// <summary>Authority history.</summary>
    public static readonly EndpointDefinition AuthHistoryAllHistory = new()
    {
        ResourceName = "9mw4-x3tu.json", FriendlyName = "AuthHistoryAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Authority history."
    };

    /// <summary>BOC-3 process agent history.</summary>
    public static readonly EndpointDefinition Boc3AllHistory = new()
    {
        ResourceName = "2emp-mxtb.json", FriendlyName = "Boc3AllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "BOC-3 process agent history."
    };

    /// <summary>Carrier registration history.</summary>
    public static readonly EndpointDefinition CarrierAllHistory = new()
    {
        ResourceName = "6eyk-hxee.json", FriendlyName = "CarrierAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Carrier registration history."
    };

    /// <summary>Company census data.</summary>
    public static readonly EndpointDefinition CompanyCensus = new()
    {
        ResourceName = "az4n-8mr2.json", FriendlyName = "CompanyCensus", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Company census data."
    };

    /// <summary>Crash file data.</summary>
    public static readonly EndpointDefinition CrashFile = new()
    {
        ResourceName = "aayw-vxb3.json", FriendlyName = "CrashFile", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Crash file data."
    };

    /// <summary>Insurance history (all with history).</summary>
    public static readonly EndpointDefinition InsHistAllWithHistory = new()
    {
        ResourceName = "6sqe-dvqs.json", FriendlyName = "InsHistAllWithHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Insurance history (all with history)."
    };

    /// <summary>Inspections and citations.</summary>
    public static readonly EndpointDefinition InspectionsAndCitations = new()
    {
        ResourceName = "qbt8-7vic.json", FriendlyName = "InspectionsAndCitations", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Inspections and citations."
    };

    /// <summary>Inspections per unit.</summary>
    public static readonly EndpointDefinition InspectionsPerUnit = new()
    {
        ResourceName = "wt8s-2hbx.json", FriendlyName = "InspectionsPerUnit", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Inspections per unit."
    };

    /// <summary>Insurance history (all).</summary>
    public static readonly EndpointDefinition InsurAllHistory = new()
    {
        ResourceName = "ypjt-5ydn.json", FriendlyName = "InsurAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Insurance history (all)."
    };

    /// <summary>Rejected applications history.</summary>
    public static readonly EndpointDefinition RejectedAllHistory = new()
    {
        ResourceName = "96tg-4mhf.json", FriendlyName = "RejectedAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Rejected applications history."
    };

    /// <summary>Revocation history.</summary>
    public static readonly EndpointDefinition RevocationAllHistory = new()
    {
        ResourceName = "sa6p-acbp.json", FriendlyName = "RevocationAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Revocation history."
    };

    /// <summary>SMS input crash data.</summary>
    public static readonly EndpointDefinition SmsInputCrash = new()
    {
        ResourceName = "4wxs-vbns.json", FriendlyName = "SmsInputCrash", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "SMS input crash data."
    };

    /// <summary>SMS input inspection data.</summary>
    public static readonly EndpointDefinition SmsInputInspection = new()
    {
        ResourceName = "rbkj-cgst.json", FriendlyName = "SmsInputInspection", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "SMS input inspection data."
    };

    /// <summary>SMS input motor carrier census.</summary>
    public static readonly EndpointDefinition SmsInputMotorCarrierCensus = new()
    {
        ResourceName = "kjg3-diqy.json", FriendlyName = "SmsInputMotorCarrierCensus", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "SMS input motor carrier census."
    };

    /// <summary>SMS input violation data.</summary>
    public static readonly EndpointDefinition SmsInputViolation = new()
    {
        ResourceName = "8mt8-2mdr.json", FriendlyName = "SmsInputViolation", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "SMS input violation data."
    };

    /// <summary>Special studies data.</summary>
    public static readonly EndpointDefinition SpecialStudies = new()
    {
        ResourceName = "5qik-smay.json", FriendlyName = "SpecialStudies", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Special studies data."
    };

    /// <summary>Vehicle inspection file.</summary>
    public static readonly EndpointDefinition VehicleInspectionFile = new()
    {
        ResourceName = "fx4q-ay7w.json", FriendlyName = "VehicleInspectionFile", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Vehicle inspection file."
    };

    /// <summary>Vehicle inspections and violations.</summary>
    public static readonly EndpointDefinition VehicleInspectionsAndViolations = new()
    {
        ResourceName = "876r-jsdb.json", FriendlyName = "VehicleInspectionsAndViolations", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple,
        Description = "Vehicle inspections and violations."
    };

    // ── Endpoint catalog ──────────────────────────────────────────────

    /// <summary>All FMCSA endpoints as CLI-ready entries (alphabetical).</summary>
    public static IReadOnlyList<EndpointEntry> All { get; } =
    [
        new(nameof(ActPendInsurAllHistory),          ActPendInsurAllHistory),
        new(nameof(AuthHistoryAllHistory),           AuthHistoryAllHistory),
        new(nameof(Boc3AllHistory),                  Boc3AllHistory),
        new(nameof(CarrierAllHistory),               CarrierAllHistory),
        new(nameof(CompanyCensus),                   CompanyCensus),
        new(nameof(CrashFile),                       CrashFile),
        new(nameof(InsHistAllWithHistory),           InsHistAllWithHistory),
        new(nameof(InspectionsAndCitations),         InspectionsAndCitations),
        new(nameof(InspectionsPerUnit),              InspectionsPerUnit),
        new(nameof(InsurAllHistory),                 InsurAllHistory),
        new(nameof(RejectedAllHistory),              RejectedAllHistory),
        new(nameof(RevocationAllHistory),            RevocationAllHistory),
        new(nameof(SmsInputCrash),                   SmsInputCrash),
        new(nameof(SmsInputInspection),              SmsInputInspection),
        new(nameof(SmsInputMotorCarrierCensus),      SmsInputMotorCarrierCensus),
        new(nameof(SmsInputViolation),               SmsInputViolation),
        new(nameof(SpecialStudies),                  SpecialStudies),
        new(nameof(VehicleInspectionFile),           VehicleInspectionFile),
        new(nameof(VehicleInspectionsAndViolations), VehicleInspectionsAndViolations),
    ];
}
