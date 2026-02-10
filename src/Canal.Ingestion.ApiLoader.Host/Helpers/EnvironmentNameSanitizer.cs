using System.Text.RegularExpressions;

namespace Canal.Ingestion.ApiLoader.Host.Helpers;

/// <summary>
/// Normalizes environment names so they are safe for use in ADLS blob paths.
/// Rules: trim, uppercase, strip anything that isn't alphanumeric / hyphen / underscore.
/// </summary>
public static partial class EnvironmentNameSanitizer
{
    public static string Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "UNDEFINED";

        var trimmed = raw.Trim().ToUpperInvariant();
        var scrubbed = SafeBlobChars().Replace(trimmed, string.Empty);
        return string.IsNullOrEmpty(scrubbed) ? "UNDEFINED" : scrubbed;
    }

    [GeneratedRegex(@"[^A-Z0-9\-_]")]
    private static partial Regex SafeBlobChars();
}
