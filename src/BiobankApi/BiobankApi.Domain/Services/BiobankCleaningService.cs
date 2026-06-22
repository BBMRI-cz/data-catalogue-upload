using System.Globalization;
using BiobankApi.Domain.Common;

namespace BiobankApi.Domain.Services;

/// <inheritdoc cref="IBiobankCleaningService"/>
public sealed class BiobankCleaningService : IBiobankCleaningService
{
    public string? CleanText(string? raw)
    {
        if (raw is null)
        {
            return null;
        }

        var stripped = raw.Trim();
        return stripped.Length == 0 ? null : stripped;
    }

    public IReadOnlyList<string> CleanAccessions(IEnumerable<string>? values)
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
    public string? CleanCode(string? raw) => CleanText(raw);

    public string? CleanSampleId(string? raw) => CleanText(raw);

    public string? DashToNone(string? raw)
    {
        // "-" is the source's "not applicable" sentinel for biopsy / predictive_number.
        var cleaned = CleanText(raw);
        return cleaned == "-" ? null : cleaned;
    }

    public bool? ParseConsent(string? raw)
    {
        var cleaned = CleanText(raw);
        if (cleaned is null)
        {
            return null;
        }

        if (string.Equals(cleaned, "true", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(cleaned, "false", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        throw new DomainException($"invalid consent value: '{raw}'");
    }

    public Sex? ParseSex(string? raw)
    {
        var cleaned = CleanText(raw);
        if (cleaned is null)
        {
            return null;
        }

        return cleaned.ToLowerInvariant() switch
        {
            "male" => Sex.Male,
            "female" => Sex.Female,
            _ => throw new DomainException($"invalid sex value: '{raw}'"),
        };
    }

    public Retrieved? ParseRetrieved(string? raw)
    {
        var cleaned = CleanText(raw);
        if (cleaned is null)
        {
            return null;
        }

        return cleaned.ToLowerInvariant() switch
        {
            "operational" => Retrieved.Operational,
            "unknown" => Retrieved.Unknown,
            _ => throw new DomainException($"invalid retrieved value: '{raw}'"),
        };
    }

    public int? ParseInt(string? raw)
    {
        var cleaned = CleanText(raw);
        return cleaned is null
            ? null
            : int.Parse(cleaned, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    public int? ParseYear(string? raw) => ParseInt(raw);

    public int? ParseMonth(string? raw)
    {
        // <patient month> is xs:gMonth ("--MM"); strip the leading "--" (a bare "7" also parses).
        var cleaned = CleanText(raw);
        if (cleaned is null)
        {
            return null;
        }

        if (cleaned.StartsWith("--", StringComparison.Ordinal))
        {
            cleaned = cleaned[2..];
        }

        return int.Parse(cleaned, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
    }

    public DateTime? ParseTemporal(string? raw)
    {
        // xs:date | xs:dateTime union: a "T" time component yields a time-of-day, otherwise midnight.
        var cleaned = CleanText(raw);
        return cleaned is null
            ? null
            : DateTime.Parse(cleaned, CultureInfo.InvariantCulture, DateTimeStyles.None);
    }
}
