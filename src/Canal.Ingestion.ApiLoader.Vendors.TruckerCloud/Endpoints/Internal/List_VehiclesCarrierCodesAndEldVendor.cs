using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Adapters.Utilities;

namespace Canal.Ingestion.ApiLoader.Vendors.TruckerCloud.Endpoints.Internal;

internal readonly record struct VehicleCarrierAndEldVendorRow(string CarrierCode, string CodeType, string EldVendor, string VehicleId, string VehicleIdType = "assetEldId");
internal static class List_VehiclesCarrierCodesAndEldVendor
{
    public static List<VehicleCarrierAndEldVendorRow> FromList(List<FetchResult> vehicles)
    {
        var pagesJson = vehicles.Select(r => r.Content).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        var mapping = new Dictionary<string, string>
        {
            ["CarrierCode"] = "carrierCodes[*].carrierCode",
            ["CodeType"] = "carrierCodes[*].codeType",
            ["EldVendor"] = "eldVendors[*].eldVendor",
            ["VehicleId"] = "assetEldId"
        };

        var rows = JsonQueryHelper.QuickQuery(pagesJson, mapping, distinct: true)
            .Where(r => r.TryGetValue("CodeType", out var t) && !string.Equals(t, "TCID", StringComparison.OrdinalIgnoreCase))
            .Where(r => r.TryGetValue("CarrierCode", out var v) && !string.IsNullOrWhiteSpace(v))
            .Where(r => r.TryGetValue("EldVendor", out var v) && !string.IsNullOrWhiteSpace(v))
            .Where(r => r.TryGetValue("VehicleId", out var v) && !string.IsNullOrWhiteSpace(v));

        return rows.Select(r => new VehicleCarrierAndEldVendorRow(r["CarrierCode"], r["CodeType"], r["EldVendor"], r["VehicleId"], "assetEldId")).ToList();
    }
}
