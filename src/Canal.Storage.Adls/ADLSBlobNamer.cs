using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Canal.Storage.Adls;

public static class ADLSBlobNamer
{
    public static string GetBlobName(BlobCategory category, string environmentName, bool dataSourceIsExternal, string ingestionDomain, string vendorName, string resourceName, int resourceVersion
                                     , string ingestionRunEpoch = "", string requestId = "", int? pageNr = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(environmentName, nameof(environmentName));
        ArgumentException.ThrowIfNullOrWhiteSpace(ingestionDomain, nameof(ingestionDomain));
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorName, nameof(vendorName));
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName, nameof(resourceName));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(resourceVersion, nameof(resourceVersion));

        if (category == BlobCategory.Content || category == BlobCategory.MetaData)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(ingestionRunEpoch, nameof(ingestionRunEpoch));
            ArgumentException.ThrowIfNullOrWhiteSpace(requestId, nameof(requestId));
            if (!pageNr.HasValue) throw new ArgumentNullException(nameof(pageNr));
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pageNr.GetValueOrDefault(), nameof(pageNr));
        }

        environmentName = AdlsPathSanitizer.SanitizePathSegment(environmentName, nameof(environmentName));
        ingestionDomain = AdlsPathSanitizer.SanitizePathSegment(ingestionDomain, nameof(ingestionDomain));
        vendorName      = AdlsPathSanitizer.SanitizePathSegment(vendorName, nameof(vendorName));
        resourceName    = AdlsPathSanitizer.SanitizePathSegment(resourceName, nameof(resourceName));

        string pageNrStr     = pageNr.GetValueOrDefault().ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
        string resourceVersionStr = resourceVersion.ToString(CultureInfo.InvariantCulture).PadLeft(4, '0');
        string internalExternal = dataSourceIsExternal ? "external" : "internal";
        
        string path = category switch
        {
            BlobCategory.Watermark => $"{environmentName}/{internalExternal}/{ingestionDomain}/{vendorName}/{resourceName}/{resourceVersionStr}",
            BlobCategory.Content   => $"{environmentName}/{internalExternal}/{ingestionDomain}/{vendorName}/{resourceName}/{resourceVersionStr}/{ingestionRunEpoch}",
            BlobCategory.MetaData  => $"{environmentName}/{internalExternal}/{ingestionDomain}/{vendorName}/{resourceName}/{resourceVersionStr}/{ingestionRunEpoch}/metadata",
                                 _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown blob category.")
        };

        string name = category switch
        {
            BlobCategory.Watermark => "ingestion_watermark.json",
                                 _ => $"{requestId}_p{pageNrStr}.json",
        };

        var resut = category switch
        {
            BlobCategory.Watermark => $"{path}/{name}",
            BlobCategory.Content   => $"{path}/data_{name}",
            BlobCategory.MetaData  => $"{path}/metadata_{name}",
                                 _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown blob category.")
        };

        return resut;
    }

    private static class AdlsPathSanitizer
    {
        public static string SanitizePathSegment(string? value, string paramName, int maxLen = 80)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);

            var s = value.Trim();

            // Prevent callers from injecting additional folders via leading/trailing separators.
            s = s.Trim('/', '\\');

            ArgumentException.ThrowIfNullOrWhiteSpace(s, paramName);

            // Preserve the intent that slashes become a visible separator token.
            s = s.Replace("/", "__").Replace("\\", "__");

            // Convert any remaining whitespace to '_' (single underscore per whitespace run).
            s = CollapseWhitespaceToUnderscore(s);

            // Replace control characters and tooling-hostile characters.
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (char.IsControl(ch)) continue;

                if (char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
                    sb.Append(ch);
                else
                    sb.Append('_');
            }

            // Keep "__" as-is, but reduce longer underscore runs (e.g., "____" -> "__").
            var sanitized = CollapseUnderscoreRunsMax2(sb.ToString()).Trim('_', '.');

            ArgumentException.ThrowIfNullOrWhiteSpace(sanitized, paramName);

            // Avoid ambiguous dot segments.
            if (sanitized is "." or "..")
                throw new ArgumentException("Invalid Parameter Value", paramName);

            if (sanitized.Length > maxLen)
                sanitized = TruncateWithHashSuffix(sanitized, maxLen);

            ArgumentException.ThrowIfNullOrWhiteSpace(sanitized, paramName);
            return sanitized;
        }

        private static string CollapseWhitespaceToUnderscore(string input)
        {
            var sb = new StringBuilder(input.Length);
            var inWs = false;

            foreach (var ch in input)
            {
                if (char.IsWhiteSpace(ch))
                {
                    if (!inWs) sb.Append('_');
                    inWs = true;
                }
                else
                {
                    sb.Append(ch);
                    inWs = false;
                }
            }

            return sb.ToString();
        }

        private static string CollapseUnderscoreRunsMax2(string input)
        {
            if (input.Length == 0) return input;

            var sb = new StringBuilder(input.Length);
            var run = 0;

            foreach (var ch in input)
            {
                if (ch == '_')
                {
                    run++;
                    if (run <= 2) sb.Append('_');
                }
                else
                {
                    run = 0;
                    sb.Append(ch);
                }
            }

            return sb.ToString();
        }

        private static string TruncateWithHashSuffix(string input, int maxLen)
        {
            const int hashChars = 10;
            const int extra = 1 + hashChars;

            if (maxLen <= extra)
                throw new ArgumentOutOfRangeException(nameof(maxLen), "maxLen is too small to truncate with a hash suffix.");

            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            var suffix = Convert.ToHexString(hash).Substring(0, hashChars).ToLowerInvariant();

            var keep = maxLen - extra;
            var prefix = input.Substring(0, keep).Trim('_', '.');

            return prefix + "_" + suffix;
        }
    }
}
