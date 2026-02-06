using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Canal.Ingestion.ApiLoader.Model;

internal sealed class FetchMetaData
{
    public FetchMetaData(FetchResult fetchResult, List<string> redactKeys)
    {
        // TODO (usability / API clarity):
        // Today we expose a single `redactKeys` list. That might be fine, but we should validate it by actually using
        // this class in a few real integrations.
        //
        // Questions to answer while using it:
        // - Is a single list intuitive, or does it make callers guess where a key might appear?
        // - Would separate lists be clearer and harder to misuse?
        //     - `headerRedactKeys`
        //     - `queryParamRedactKeys`
        //     - `requestBodyRedactKeys`
        //
        // Alternative design questions:
        // - Is a dictionary the right structure for this data?
        //     - Note: JSON serialization + attributes on poco object properties felt like overkill during the POC, but it may be worth revisiting - during POC it felt simplest to keep all metadata logic in one method - the JsonString() method below.
        //
        // Bottom line:
        // This class is intended to cover ~99% of metadata needs regardless of vendor or scenario.
        // If we discover missing metadata fields, we should add them early so "raw external data" stays as homogeneous across vendors - this simplifies data consumption (without this metadata, the raw data is often useless)
        _fetchResult = fetchResult ?? throw new ArgumentNullException(nameof(fetchResult));
        _metaDataRedactKeys = redactKeys ?? [];
    }

    private readonly FetchResult _fetchResult;
    private readonly List<string> _metaDataRedactKeys;

    public string JsonString()
    {
        var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var run = _fetchResult.IngestionRun;

        AddMetaData(d, "ingestion_run_id", run.IngestionRunId);
        AddMetaData(d, "request_id", NormalizeHash(_fetchResult.Request.RequestId));
        AddMetaData(d, "page_nr", FormatInt(_fetchResult.PageNr));
        AddMetaData(d, "attempt_nr", FormatInt(_fetchResult.AttemptNr));

        AddMetaData(d, "page_id", _fetchResult.PageId);
        AddMetaData(d, "attempt_id", _fetchResult.Request.AttemptId);
        AddMetaData(d, "request_uri", _fetchResult.RequestUri?.ToString());

        AddMetaData(d, "ingestion_domain", run.IngestionDomain);
        AddMetaData(d, "source_vendor", run.VendorName);

        AddMetaData(d, "work_vendor", _fetchResult.Request.VendorName);
        AddMetaData(d, "resource_name_friendly", _fetchResult.Request.ResourceNameFriendly);
        AddMetaData(d, "resource_name", _fetchResult.Request.ResourceName);
        AddMetaData(d, "resource_version", _fetchResult.Request.ResourceVersion.ToString());

        AddNestedMetaData(d, "query_parameters", _fetchResult.Request.QueryParameters);

        // Prefer the headers that were actually applied to the HttpRequestMessage for this attempt.
        // (The Request.RequestHeaders are the caller-provided 'intent', and may be empty.)
        var headers = (_fetchResult.EffectiveRequestHeaders?.Count ?? 0) > 0
            ? _fetchResult.EffectiveRequestHeaders
            : _fetchResult.Request.RequestHeaders;

        AddNestedMetaData(d, "request_headers", headers);

        AddMetaData(d, "http_status_code", _fetchResult.HttpStatusCode?.ToString());
        AddMetaData(d, "fetch_outcome", _fetchResult.FetchOutcome.ToString());

        AddMetaData(d, "total_pages", FormatInt(_fetchResult.TotalPages));
        AddMetaData(d, "total_element_count", FormatInt(_fetchResult.TotalElements));

        AddMetaData(d, "payload_sha256", NormalizeHash(_fetchResult.PayloadSha256));
        AddMetaData(d, "payload_bytes", FormatLong(_fetchResult.PayloadBytes));

        AddMetaData(d, "ingestion_run_started_utc", FormatUtc(run.IngestionRunStartUtc));

        AddMetaData(d, "requested_utc", FormatUtc(_fetchResult.RequestedUtc));
        AddMetaData(d, "response_utc", FormatUtc(_fetchResult.ReceivedUtc));
        AddMetaData(d, "response_time_ms", FormatLong(_fetchResult.ResponseTimeMs));

        AddMetaData(d, "content_type", _fetchResult.ContentType);
        AddMetaData(d, "content_encoding", _fetchResult.ContentEncoding);

        AddMetaData(d, "number_of_attempts", FormatInt(_fetchResult.NrAttempts));
        AddNestedMetaData(d, "failures", _fetchResult.Failures);

        return JsonSerializer.Serialize(d);
    }

    private static void AddMetaData(IDictionary<string, object?> d, string key, string? value)
    {
        d[key] = value ?? string.Empty; 
    }

    private void AddNestedMetaData(IDictionary<string, object?> d, string key, IReadOnlyDictionary<string, string>? map)
    {
        if (map is null)
        {
            d[key] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            return;
        }

        var redact = _metaDataRedactKeys is null ? null : new HashSet<string>(_metaDataRedactKeys, StringComparer.OrdinalIgnoreCase);
        var nested = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (k, v) in map)
        {
            var safeVal = (redact?.Contains(k) ?? false) ? "***REDACTED***" : (v ?? string.Empty);

            nested[k] = safeVal;
        }

        if (nested.Count > 0) d[key] = nested;
    }

    private static void AddNestedMetaData<T>(IDictionary<string, object?> d, string key, IEnumerable<T>? items)
    {
        if (items is null)
        {
            d[key] = new List<object?>();
            return;
        }

        var list = new List<object?>();

        foreach (var item in items)
        {
            if (item is null) continue;

            var formattedObj = FormatObjectToDictionary(item);
            if (formattedObj.Count > 0) list.Add(formattedObj);
        }

        if (list.Count > 0) d[key] = list;
    }

    private static Dictionary<string, object?> FormatObjectToDictionary(object obj)
    {
        var props = obj.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
        var nested = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in props)
        {
            if (!p.CanRead) continue;

            var raw = p.GetValue(obj);
            var formatted = FormatValue(raw);

            nested[ToSnakeCase(p.Name)] = formatted;
        }

        return nested;
    }

    private static string FormatValue(object? value)
    {
        if (value is null) return string.Empty;

        var t = value.GetType();
        if (t.IsGenericType && t.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            var hasValue = (bool)t.GetProperty("HasValue")!.GetValue(value)!;
            if (!hasValue) return string.Empty;
            value = t.GetProperty("Value")!.GetValue(value);
            if (value is null) return string.Empty;
        }

        return value switch
        {
            DateTimeOffset dto => dto.UtcDateTime.ToString("O", CultureInfo.InvariantCulture),
            DateTime dt => (dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime()).ToString("O", CultureInfo.InvariantCulture),

            Enum e => e.ToString(),

            Uri u => u.ToString(),
            Guid g => g.ToString("D"),

            bool b => b ? "true" : "false",

            byte n => n.ToString(CultureInfo.InvariantCulture),
            short n => n.ToString(CultureInfo.InvariantCulture),
            int n => n.ToString(CultureInfo.InvariantCulture),
            long n => n.ToString(CultureInfo.InvariantCulture),
            float n => n.ToString("R", CultureInfo.InvariantCulture),
            double n => n.ToString("R", CultureInfo.InvariantCulture),
            decimal n => n.ToString(CultureInfo.InvariantCulture),

            string s => s,

            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static string ToSnakeCase(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;

        var sb = new StringBuilder(s.Length + 8);

        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];

            if (char.IsUpper(c))
            {
                bool hasPrev = i > 0;
                bool prevIsLowerOrDig = hasPrev && (char.IsLower(s[i - 1]) || char.IsDigit(s[i - 1]));
                bool nextIsLower = (i + 1 < s.Length) && char.IsLower(s[i + 1]);

                if (hasPrev && (prevIsLowerOrDig || nextIsLower)) sb.Append('_');

                sb.Append(char.ToLowerInvariant(c));
            }
            else
            {
                sb.Append(c);
            }
        }

        return sb.ToString();
    }

    private static string? FormatUtc(DateTimeOffset? dto) => dto?.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
    private static string? FormatInt(int? n) => n?.ToString(CultureInfo.InvariantCulture);
    private static string? FormatLong(long? n) => n?.ToString(CultureInfo.InvariantCulture);
    private static string? NormalizeHash(string? hex) => string.IsNullOrWhiteSpace(hex) ? null : hex.Trim().ToLowerInvariant();
}