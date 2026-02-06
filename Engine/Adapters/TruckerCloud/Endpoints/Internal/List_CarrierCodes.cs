using Canal.Ingestion.ApiLoader.Model;
using Canal.Ingestion.ApiLoader.Engine.Adapters.Utilities;

namespace Canal.Ingestion.ApiLoader.Engine.Adapters.TruckerCloud.Endpoints.Internal;

internal readonly record struct CarrierCodeAndTypeRow(string CarrierCodeType, string CarrierCode);
internal static class List_CarrierCodes
{
    public static List<CarrierCodeAndTypeRow> FromList(List<FetchResult> carriers)
    {
        var pagesJson = carriers.Select(r => r.Content).Where(s => !string.IsNullOrWhiteSpace(s)).ToList();

        var mapping = new Dictionary<string, string>
        {
            ["CarrierCodeType"] = "carrierInfo.carrierInfoCodes[*].codeType",
            ["CarrierCode"] = "carrierInfo.carrierInfoCodes[*].carrierCode"
        };

        var rows = JsonQueryHelper.QuickQuery(pagesJson, mapping, distinct: true)
            .Where(r => r.TryGetValue("CarrierCodeType", out var t) && !string.Equals(t, "TCID", StringComparison.OrdinalIgnoreCase));

        return rows.Select(r => new CarrierCodeAndTypeRow(r["CarrierCodeType"], r["CarrierCode"])).ToList();
    }
}