namespace Uploader.Application.Mapping;

/// <summary>
/// The computed pieces of the biobank -> domain mapping. Everything else is carried verbatim, so this
/// is the whole of the mapping's logic: the derived clinical identifier, the ICD-10 dot rule and the
/// age computation, all three ported from the previous production uploader. Catalogue vocabulary
/// (MOLGENIS lookup strings, nullflavors) is deliberately absent — that belongs to the catalogue
/// gateway, not here.
/// </summary>
internal static class BiobankMapping
{
    /// <summary>The biobank's discriminator for tissue samples.</summary>
    public const string TissueType = "tissue";

    /// <summary>The biobank's discriminator for genome samples.</summary>
    public const string GenomeType = "genome";

    public static bool IsTissue(string? sampleType) =>
        string.Equals(sampleType, TissueType, StringComparison.OrdinalIgnoreCase);

    public static bool IsGenome(string? sampleType) =>
        string.Equals(sampleType, GenomeType, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Identifier of the patient's single clinical record, derived from the patient id. Once the
    /// pseudonymized ids land (#80/S3) a <c>mmci_patient_&lt;uuid&gt;</c> becomes
    /// <c>mmci_clinical_&lt;uuid&gt;</c>, which is what the previous uploader produced; a raw biobank
    /// id just gets the prefix.
    /// </summary>
    public static string? ClinicalIdentifier(string? patientId)
    {
        if (string.IsNullOrWhiteSpace(patientId))
        {
            return null;
        }

        return patientId.Contains("patient", StringComparison.Ordinal)
            ? patientId.Replace("patient", "clinical", StringComparison.Ordinal)
            : $"clinical_{patientId}";
    }

    /// <summary>
    /// The biobank exports ICD-10 dot-less (<c>C504</c>); the catalogue's code lists spell the
    /// subcategory out (<c>C50.4</c>). Only a 4-character code has a subcategory to separate.
    /// </summary>
    public static string? Diagnosis(string? code)
    {
        var trimmed = code?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        return trimmed.Length == 4 ? $"{trimmed[..3]}.{trimmed[3]}" : trimmed;
    }

    /// <summary>
    /// Whole years between the patient's birth and an event. The export carries no birth day, and
    /// often no month either, so the birth date is taken as the first of the (defaulted) month —
    /// the previous uploader's rule.
    /// </summary>
    public static int? AgeInYears(int? birthYear, int? birthMonth, DateTime? at)
    {
        if (birthYear is not { } year || at is not { } moment)
        {
            return null;
        }

        var month = birthMonth is >= 1 and <= 12 ? birthMonth.Value : 1;
        var birth = new DateTime(year, month, 1);

        var age = moment.Year - birth.Year;
        if (moment < birth.AddYears(age))
        {
            age--;
        }

        // An event before the birth date is contradictory source data, not an age.
        return age < 0 ? null : age;
    }
}
