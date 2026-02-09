using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Engine;

namespace Canal.Ingestion.ApiLoader.Engine.Adapters.Fmcsa;

public static class FmcsaEndpoints
{
    public static readonly EndpointDefinition ActPendInsurAllHistory = new()
    {
        ResourceName = "qh9u-swkp.json", FriendlyName = "ActPendInsurAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition AuthHistoryAllHistory = new()
    {
        ResourceName = "9mw4-x3tu.json", FriendlyName = "AuthHistoryAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition Boc3AllHistory = new()
    {
        ResourceName = "2emp-mxtb.json", FriendlyName = "Boc3AllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition CarrierAllHistory = new()
    {
        ResourceName = "6eyk-hxee.json", FriendlyName = "CarrierAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition CompanyCensus = new()
    {
        ResourceName = "az4n-8mr2.json", FriendlyName = "CompanyCensus", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition CrashFile = new()
    {
        ResourceName = "aayw-vxb3.json", FriendlyName = "CrashFile", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition InsHistAllWithHistory = new()
    {
        ResourceName = "6sqe-dvqs.json", FriendlyName = "InsHistAllWithHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition InspectionsAndCitations = new()
    {
        ResourceName = "qbt8-7vic.json", FriendlyName = "InspectionsAndCitations", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition InspectionsPerUnit = new()
    {
        ResourceName = "wt8s-2hbx.json", FriendlyName = "InspectionsPerUnit", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition InsurAllHistory = new()
    {
        ResourceName = "ypjt-5ydn.json", FriendlyName = "InsurAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition RejectedAllHistory = new()
    {
        ResourceName = "96tg-4mhf.json", FriendlyName = "RejectedAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition RevocationAllHistory = new()
    {
        ResourceName = "sa6p-acbp.json", FriendlyName = "RevocationAllHistory", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition SmsInputCrash = new()
    {
        ResourceName = "4wxs-vbns.json", FriendlyName = "SmsInputCrash", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition SmsInputInspection = new()
    {
        ResourceName = "rbkj-cgst.json", FriendlyName = "SmsInputInspection", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition SmsInputMotorCarrierCensus = new()
    {
        ResourceName = "kjg3-diqy.json", FriendlyName = "SmsInputMotorCarrierCensus", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition SmsInputViolation = new()
    {
        ResourceName = "8mt8-2mdr.json", FriendlyName = "SmsInputViolation", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition SpecialStudies = new()
    {
        ResourceName = "5qik-smay.json", FriendlyName = "SpecialStudies", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition VehicleInspectionFile = new()
    {
        ResourceName = "fx4q-ay7w.json", FriendlyName = "VehicleInspectionFile", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };

    public static readonly EndpointDefinition VehicleInspectionsAndViolations = new()
    {
        ResourceName = "876r-jsdb.json", FriendlyName = "VehicleInspectionsAndViolations", ResourceVersion = 1,
        DefaultPageSize = 500,
        BuildRequests = RequestBuilders.Simple
    };
}
