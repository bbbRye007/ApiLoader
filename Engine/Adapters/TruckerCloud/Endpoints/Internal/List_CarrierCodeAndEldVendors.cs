using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Adapters.Utilities;

namespace Canal.Ingestion.ApiLoader.Adapters.TruckerCloud.Endpoints.Internal;

internal readonly record struct CarrierCodeAndEldVendorRow(string CarrierCodeType, string CarrierCode, string EldVendor);
internal static class List_CarrierCodesAndEldVendors
{
    public static List<CarrierCodeAndEldVendorRow> FromList(List<FetchResult> carriers)
    {
        var pagesJson = carriers.Select(r => r.Content).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        var mapping = new Dictionary<string, string>
        {
            ["CarrierCodeType"] = "carrierInfo.carrierInfoCodes[*].codeType",
            ["CarrierCode"] = "carrierInfo.carrierInfoCodes[*].carrierCode",
            ["EldVendor"] = "eldVendorInfo.[*].eldVendor"
        };

        var rows = JsonQueryHelper.QuickQuery(pagesJson, mapping, distinct: true)
            .Where(r => r.TryGetValue("CarrierCodeType", out var t) && !string.Equals(t, "TCID", StringComparison.OrdinalIgnoreCase));

        return rows.Select(r => new CarrierCodeAndEldVendorRow(r["CarrierCodeType"], r["CarrierCode"], r["EldVendor"])).ToList();
    }
}