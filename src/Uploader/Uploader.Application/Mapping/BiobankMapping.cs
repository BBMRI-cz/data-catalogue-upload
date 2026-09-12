namespace Uploader.Application.Mapping;

/// <summary>
/// The computed pieces of the biobank -> domain mapping. Everything else is carried verbatim, so this
/// is the whole of the mapping's logic: the derived clinical identifier, the ICD-10 dot rule and the
/// age computation, all three ported from the previous production uploader. The catalogue's
/// controlled terms live next door in <see cref="CatalogueVocabulary"/>, which is where a biobank
/// code becomes an ontology term.
/// </summary>
public static class BiobankMapping
{
    /// <summary>The biobank's discriminator for tissue samples.</summary>
    public const string TissueType = "tissue";

    /// <summary>The biobank's discriminator for serum samples.</summary>
    public const string SerumType = "serum";

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

        return Derive(patientId, "patient", "clinical");
    }

    /// <summary>
    /// Identifier of the biospecimen stored from a sample, derived from the sample id the same way
    /// the clinical identifier is derived from the patient id. Material and biospecimen are two
    /// catalogue rows describing one archived sample, so they cannot share a key.
    /// </summary>
    public static string? BiospecimenIdentifier(string? sampleId) => Derive(sampleId, "sample", "biospecimen");

    /// <summary>
    /// Identifier of the patient's consent record, derived from the patient id. The biobank exports
    /// a boolean, not a signed form, so there is no consent id of its own to carry.
    /// </summary>
    public static string? ConsentIdentifier(string? patientId) => Derive(patientId, "patient", "consent");

    /// <summary>
    /// One derived identifier from another: a pseudonym already reads <c>mmci_&lt;kind&gt;_&lt;uuid&gt;</c>,
    /// so swapping the kind keeps the two rows recognisably the same sample or person. A raw
    /// biobank id has no kind in it and just gets the new one as a prefix.
    /// </summary>
    private static string? Derive(string? id, string from, string to)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        return id.Contains(from, StringComparison.Ordinal)
            ? id.Replace(from, to, StringComparison.Ordinal)
            : $"{to}_{id}";
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
