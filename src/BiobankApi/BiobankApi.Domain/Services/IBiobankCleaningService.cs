namespace BiobankApi.Domain.Services;

/// <summary>
/// Domain service that turns raw biobank XML strings into cleaned, typed domain values
/// (the C# home of the Python <c>domain.cleaning</c> module). It owns the field-level
/// parsing/validation rules; the XML parser (infrastructure) composes these into
/// <see cref="Patients.Patient"/> aggregates, whose constructors then enforce invariants.
/// All methods are pure and deterministic.
/// </summary>
public interface IBiobankCleaningService
{
    /// <summary>Trim whitespace; an empty result becomes <c>null</c>.</summary>
    string? CleanText(string? raw);

    /// <summary>Clean and drop empty values from a list of accession numbers.</summary>
    IReadOnlyList<string> CleanAccessions(IEnumerable<string>? values);

    /// <summary>Diagnosis/morphology/pTNM codes — kept exactly as exported (just trimmed).</summary>
    string? CleanCode(string? raw);

    /// <summary>Clean a sample id (the "&amp;" entity is already decoded by the XML reader).</summary>
    string? CleanSampleId(string? raw);

    /// <summary>Map the source "-" not-applicable sentinel to <c>null</c>.</summary>
    string? DashToNone(string? raw);

    /// <summary>Parse a <c>true</c>/<c>false</c> consent flag.</summary>
    bool? ParseConsent(string? raw);

    /// <summary>Parse a <c>male</c>/<c>female</c> sex value.</summary>
    Sex? ParseSex(string? raw);

    /// <summary>Parse an <c>operational</c>/<c>unknown</c> retrieved value.</summary>
    Retrieved? ParseRetrieved(string? raw);

    /// <summary>Parse a base-10 integer.</summary>
    int? ParseInt(string? raw);

    /// <summary>Parse a gYear into an integer.</summary>
    int? ParseYear(string? raw);

    /// <summary>Parse a gMonth ("--MM" or bare "M") into 1–12.</summary>
    int? ParseMonth(string? raw);

    /// <summary>Parse an xs:date or xs:dateTime (a "T" component yields a time-of-day).</summary>
    DateTime? ParseTemporal(string? raw);
}
