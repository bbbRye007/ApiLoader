using System.Text.Json;

namespace Canal.Ingestion.ApiLoader.Engine.Adapters.Utilities;
internal static class JsonQueryHelper
{
    internal static List<Dictionary<string, string>> QuickQuery(IEnumerable<string> pagesJson, IReadOnlyDictionary<string, string> columnMap, bool distinct, string contentPropertyName = "content")
    {
        if (pagesJson is null) throw new ArgumentNullException(nameof(pagesJson));
        if (columnMap is null) throw new ArgumentNullException(nameof(columnMap));
        if (columnMap.Count == 0) return new();

        var results = new List<Dictionary<string, string>>();
        var distinctSet = distinct ? new HashSet<string>(StringComparer.Ordinal) : null;

        foreach (var pageJson in pagesJson)
        {
            if (string.IsNullOrWhiteSpace(pageJson)) continue;

            using var doc = JsonDocument.Parse(pageJson);
            var root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Object)
                return results;

            if (!root.TryGetProperty(contentPropertyName, out var content) || content.ValueKind != JsonValueKind.Array)
                return results;

            foreach (var rowObj in content.EnumerateArray())
            {
                foreach (var row in ExpandOneRow(rowObj, columnMap))
                {
                    if (distinctSet is null)
                    {
                        results.Add(row);
                        continue;
                    }

                    var key = BuildDistinctKey(row, columnMap.Keys);
                    if (distinctSet.Add(key))
                        results.Add(row);
                }
            }
        }

        return results;
    }

    private static IEnumerable<Dictionary<string, string>> ExpandOneRow(JsonElement rowObj, IReadOnlyDictionary<string, string> columnDefs)
    {
        var scalarCols = new List<(string Col, string Path)>();
        var arrayCols = new List<(string Col, string Prefix, string Suffix)>();

        foreach (var kvp in columnDefs)
        {
            var colName = kvp.Key;
            var path = kvp.Value ?? string.Empty;

            var starIdx = path.IndexOf("[*]", StringComparison.Ordinal);
            if (starIdx < 0) scalarCols.Add((colName, path));
            else
            {
                var prefix = path.Substring(0, starIdx + 3);
                var suffix = path[(starIdx + 3)..];
                arrayCols.Add((colName, prefix, suffix));
            }
        }

        var baseRow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (col, path) in scalarCols)
            baseRow[col] = GetScalar(rowObj, path);

        if (arrayCols.Count == 0)
        {
            yield return baseRow;
            yield break;
        }

        var byPrefix = arrayCols.GroupBy(a => a.Prefix).ToList();

        var expanded = new List<Dictionary<string, string>> { baseRow };

        foreach (var group in byPrefix)
        {
            var prefix = group.Key;
            var cols = group.ToList();

            var arrayValuesByCol = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            var maxLen = 0;

            foreach (var (col, _, suffix) in cols)
            {
                var arr = GetArray(rowObj, prefix);
                var values = new List<string>();

                foreach (var el in arr)
                {
                    var v = string.IsNullOrWhiteSpace(suffix) ? GetElementString(el) : GetScalar(el, suffix.TrimStart('.'));
                    values.Add(v);
                }

                arrayValuesByCol[col] = values;
                maxLen = Math.Max(maxLen, values.Count);
            }

            var nextExpanded = new List<Dictionary<string, string>>();

            for (var i = 0; i < Math.Max(1, maxLen); i++)
            {
                foreach (var existing in expanded)
                {
                    var clone = new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);
                    foreach (var (col, _, _) in cols)
                    {
                        var values = arrayValuesByCol[col];
                        clone[col] = i < values.Count ? values[i] : string.Empty;
                    }
                    nextExpanded.Add(clone);
                }
            }

            expanded = nextExpanded;
        }

        foreach (var row in expanded)
            yield return row;
    }

    private static string BuildDistinctKey(Dictionary<string, string> row, IEnumerable<string> cols)
        => string.Join('\u001F', cols.Select(c => row.TryGetValue(c, out var v) ? v : string.Empty));

    private static string GetScalar(JsonElement root, string dotPath)
    {
        if (string.IsNullOrWhiteSpace(dotPath)) return string.Empty;

        var parts = dotPath.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cur = root;

        foreach (var part in parts)
        {
            if (cur.ValueKind != JsonValueKind.Object) return string.Empty;
            if (!cur.TryGetProperty(part, out cur)) return string.Empty;
        }

        return GetElementString(cur);
    }

    private static IEnumerable<JsonElement> GetArray(JsonElement root, string prefixPathWithStar)
    {
        var path = prefixPathWithStar.Replace("[*]", "", StringComparison.Ordinal);
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var cur = root;
        foreach (var part in parts)
        {
            if (cur.ValueKind != JsonValueKind.Object) return Array.Empty<JsonElement>();
            if (!cur.TryGetProperty(part, out cur)) return Array.Empty<JsonElement>();
        }

        return cur.ValueKind == JsonValueKind.Array ? cur.EnumerateArray() : Array.Empty<JsonElement>();
    }

    private static string GetElementString(JsonElement el)
        => el.ValueKind switch
        {
            JsonValueKind.String => el.GetString() ?? string.Empty,
            JsonValueKind.Number => el.ToString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => el.ToString()
        };
}

