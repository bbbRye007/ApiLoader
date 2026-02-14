using System.Globalization;

namespace Canal.Ingestion.ApiLoader.Hosting.Helpers;

/// <summary>
/// Parses a variety of common date/datetime string formats into <see cref="DateTimeOffset"/>.
/// The caller should not need to worry about ISO 8601 T/Z formatting.
/// </summary>
public static class FlexibleDateParser
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-ddTHH:mm:sszzz",
        "yyyy-MM-ddTHH:mm:ss.fffzzz",
        "yyyy-MM-ddTHH:mm:ssZ",
        "yyyy-MM-ddTHH:mm:ss.fffZ",
        "yyyy-MM-ddTHH:mm:ss",
        "yyyy-MM-ddTHH:mm:ss.fff",
        "yyyy-MM-ddTHH:mm",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss.fff",
        "yyyy-MM-dd HH:mm",
        "yyyy-MM-dd",
        "M/d/yyyy h:mm:ss tt",
        "M/d/yyyy HH:mm:ss",
        "M/d/yyyy HH:mm",
        "M/d/yyyy h:mm tt",
        "M/d/yyyy",
        "MM/dd/yyyy",
    ];

    public static DateTimeOffset Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new FormatException("Date string is empty or null.");

        input = input.Trim();

        if (DateTimeOffset.TryParseExact(input, Formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var exact))
            return exact;

        if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out var general))
            return general;

        throw new FormatException(
            $"Could not parse '{input}' as a date. " +
            "Accepted formats: 2026-01-15, 2026-01-15 14:30, 01/15/2026, 2026-01-15T14:30:00Z, etc.");
    }

    public static DateTimeOffset? TryParse(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        try { return Parse(input); }
        catch (FormatException) { return null; }
    }
}
