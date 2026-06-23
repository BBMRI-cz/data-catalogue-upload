using System.Globalization;
using BiobankApi.Domain;
using ErrorOr;

namespace BiobankApi.Infrastructure.Xml;

/// <summary>
/// Source-format decoding for the biobank XML exports: trims/normalizes raw attribute and element
/// text and parses the export's lexical formats (<c>xs:gMonth</c>, <c>xs:date</c>/<c>xs:dateTime</c>,
/// the <c>"-"</c> "not applicable" sentinel, <c>true</c>/<c>false</c>). This is an XML-format concern,
/// not domain behaviour, so it lives in the source layer (analogous to a JSON → DTO decode step);
/// the domain only validates invariants via the aggregates' <c>Create</c> factories. Parse failures
/// surface as <see cref="Error.Validation(string,string,System.Collections.Generic.Dictionary{string,object}?)"/>
/// rather than exceptions.
/// </summary>
internal static class XmlValueReader
{
    public static string? CleanText(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        var stripped = raw.Trim();
        return stripped.Length == 0 ? null : stripped;
    }

    public static IReadOnlyList<string> CleanAccessions(IEnumerable<string>? values)
    {
        if (values is null)
        {
            return [];
        }

        var cleaned = new List<string>();
        foreach (var value in values)
        {
            if (CleanText(value) is { } text)
            {
                cleaned.Add(text);
            }
        }

        return cleaned;
    }

    // Diagnosis/morphology/pTNM codes are kept exactly as exported (e.g. ICD-10 stays dot-less).
    public static string? CleanCode(string? raw) => CleanText(raw);

    public static string? CleanSampleId(string? raw) => CleanText(raw);

    public static string? DashToNone(string? raw)
    {
        // "-" is the source's "not applicable" sentinel for biopsy / predictive_number.
        var cleaned = CleanText(raw);
        return cleaned == "-" ? null : cleaned;
    }

    public static ErrorOr<bool?> ParseConsent(string? raw)
    {
        var cleaned = CleanText(raw);
        if (cleaned is null)
        {
            return (bool?)null;
        }

        if (string.Equals(cleaned, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(cleaned, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Error.Validation("Xml.Consent", $"invalid consent value: '{raw}'");
    }

    public static ErrorOr<Sex?> ParseSex(string? raw)
    {
        var cleaned = CleanText(raw);
        if (cleaned is null)
        {
            return (Sex?)null;
        }

        return cleaned.ToLowerInvariant() switch
        {
            "male" => Sex.Male,
            "female" => Sex.Female,
            _ => Error.Validation("Xml.Sex", $"invalid sex value: '{raw}'"),
        };
    }

    public static ErrorOr<Retrieved?> ParseRetrieved(string? raw)
    {
        var cleaned = CleanText(raw);
        if (cleaned is null)
        {
            return (Retrieved?)null;
        }

        return cleaned.ToLowerInvariant() switch
        {
            "operational" => Retrieved.Operational,
            "unknown" => Retrieved.Unknown,
            _ => Error.Validation("Xml.Retrieved", $"invalid retrieved value: '{raw}'"),
        };
    }

    public static ErrorOr<int?> ParseInt(string? raw)
    {
        var cleaned = CleanText(raw);
        if (cleaned is null)
        {
            return (int?)null;
        }

        return int.TryParse(cleaned, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : Error.Validation("Xml.Integer", $"invalid integer value: '{raw}'");
    }

    public static ErrorOr<int?> ParseYear(string? raw) => ParseInt(raw);

    public static ErrorOr<int?> ParseMonth(string? raw)
    {
        // <patient month> is xs:gMonth ("--MM"); strip the leading "--" (a bare "7" also parses).
        var cleaned = CleanText(raw);
        if (cleaned is null)
        {
            return (int?)null;
        }

        if (cleaned.StartsWith("--", StringComparison.Ordinal))
        {
            cleaned = cleaned[2..];
        }

        return int.TryParse(cleaned, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
            ? value
            : Error.Validation("Xml.Month", $"invalid month value: '{raw}'");
    }

    public static ErrorOr<DateTime?> ParseTemporal(string? raw)
    {
        // xs:date | xs:dateTime union: a "T" time component yields a time-of-day, otherwise midnight.
        var cleaned = CleanText(raw);
        if (cleaned is null)
        {
            return (DateTime?)null;
        }

        return DateTime.TryParse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value)
            ? value
            : Error.Validation("Xml.Temporal", $"invalid date/time value: '{raw}'");
    }
}
