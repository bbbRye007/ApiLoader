using Canal.Ingestion.ApiLoader.Adapters;
using Canal.Ingestion.ApiLoader.Model;

namespace Canal.Ingestion.ApiLoader.Engine;

public static class RequestBuilders
{
    /// <summary>
    /// Single request, no prior data needed. Used by simple paged endpoints (Carriers, Vehicles, all FMCSA, etc).
    /// </summary>
    public static List<Request> Simple(IVendorAdapter adapter, EndpointDefinition def, int? pageSize, LoadParameters _)
    {
        return [new Request(adapter, def.ResourceName, def.ResourceVersion, pageSize: pageSize, httpMethod: def.HttpMethod)];
    }

    /// <summary>
    /// One request per row extracted from prior results. The extractRows function returns query-parameter dictionaries.
    /// Used by endpoints like Drivers, RiskScores where each carrier gets its own request.
    /// </summary>
    public static BuildRequestsDelegate CarrierDependent(Func<List<FetchResult>, List<Dictionary<string, string>>> extractRows)
    {
        return (adapter, def, pageSize, parameters) =>
        {
            ArgumentNullException.ThrowIfNull(parameters.IterationList, nameof(parameters.IterationList));
            var rows = extractRows(parameters.IterationList);
            return rows.Select(qp => new Request(adapter, def.ResourceName, def.ResourceVersion, pageSize: pageSize, httpMethod: def.HttpMethod, queryParameters: qp, bodyParamsJson: parameters.BodyParamsJson)).ToList();
        };
    }

    /// <summary>
    /// One request per row extracted from prior results, with time window parameters added to each request.
    /// Used by endpoints like SafetyEvents, GpsMiles, RadiusOfOperation, ZipCodeMiles.
    /// </summary>
    public static BuildRequestsDelegate CarrierAndTimeWindow(Func<List<FetchResult>, List<Dictionary<string, string>>> extractRows, string startParamName = "startTime", string endParamName = "endTime", string timeFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'")
    {
        return (adapter, def, pageSize, parameters) =>
        {
            ArgumentNullException.ThrowIfNull(parameters.IterationList, nameof(parameters.IterationList));
            var rows = extractRows(parameters.IterationList);
            return rows.Select(qp =>
            {
                qp[startParamName] = parameters.StartUtc!.Value.ToString(timeFormat);
                qp[endParamName] = parameters.EndUtc!.Value.ToString(timeFormat);
                return new Request(adapter, def.ResourceName, def.ResourceVersion, pageSize: pageSize, httpMethod: def.HttpMethod, queryParameters: qp, bodyParamsJson: parameters.BodyParamsJson);
            }).ToList();
        };
    }
}
