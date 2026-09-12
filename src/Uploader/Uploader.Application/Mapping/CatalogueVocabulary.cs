using System.Collections.Frozen;

namespace Uploader.Application.Mapping;

/// <summary>
/// The biobank's codes translated into the terms the catalogue's ontology tables actually contain.
/// <para>
/// EMX2 enforces these as foreign keys: a term that is not already in the ontology rejects the
/// whole row, transactionally (see <c>docs/catalogue-api-contract.md</c>). So a code only appears
/// here when there is a term that genuinely means the same thing. Everything else maps to
/// <c>null</c> and the column is omitted - an absent value is honest, and a wrong one would be a
/// clinical claim nobody made.
/// </para>
/// <para>
/// Every term below was verified present in <c>FairGenomesTest</c> on 2026-09-08. The gateway
/// re-checks them at run time against the live ontology, so a schema that drops or renames a term
/// costs one column rather than the row.
/// </para>
/// </summary>
public static class CatalogueVocabulary
{
    private const string SolidTissue = "Solid Tissue Specimen";
    private const string PeripheralBlood = "Peripheral Blood";

    /// <summary>
    /// <c>Material.MaterialType</c>: what was taken from the patient, before any processing.
    /// Tissue codes are all surgical tissue; every blood-derived code is a blood draw, and which
    /// fraction was kept is <see cref="BiospecimenForm"/>'s business, not this column's.
    /// </summary>
    public static string? MaterialType(string? sampleType, string? code) => (sampleType, code) switch
    {
        (BiobankMapping.TissueType, "7") => PeripheralBlood, // mononuclear cells, drawn not excised
        (BiobankMapping.TissueType, _) => SolidTissue,

        // "Primary cell cultures" is not a collected material and has no term; the rest are blood.
        (BiobankMapping.SerumType, "PR") => null,
        (BiobankMapping.SerumType, _) => PeripheralBlood,
        (BiobankMapping.GenomeType, _) => PeripheralBlood,
        _ => null,
    };

    /// <summary>
    /// <c>Material.PathologicalState</c>. The ontology offers only Normal, Tumor, Organoid and
    /// Tumoroid, so benign and premalignant tissue have nowhere to go: calling either of them
    /// "Tumor" would assert a malignancy the biobank did not record.
    /// </summary>
    public static string? PathologicalState(string? sampleType, string? code) =>
        sampleType == BiobankMapping.TissueType
            ? code switch
            {
                "1" or "53" => "Tumor",   // malignant tumour, plain and RNAlater
                "2" or "55" => "Tumor",   // metastasis
                "4" or "54" => "Normal",  // healthy tissue
                _ => null,                // benign (3, 56), premalignant (5), PBMNC (7)
            }
            : null;

    /// <summary>
    /// <c>Biospecimens.BiospecimenForm</c>: the form the biobank actually stores.
    /// <para>
    /// Tissue is frozen - the export carries a freeze time for every tissue sample, which is the
    /// evidence for it. The plasma and serum codes differ in anticoagulant, which the ontology does
    /// not distinguish below "Serum or Plasma".
    /// </para>
    /// </summary>
    public static string? BiospecimenForm(string? sampleType, string? code) => (sampleType, code) switch
    {
        (BiobankMapping.TissueType, "7") => null, // mononuclear cells are not a listed form
        (BiobankMapping.TissueType, _) => "Frozen Tissue",

        (BiobankMapping.SerumType, "PR") => null, // primary cell cultures
        (BiobankMapping.SerumType, _) => "Serum or Plasma",

        (BiobankMapping.GenomeType, "gD") => "Blood DNA",
        (BiobankMapping.GenomeType, _) => "Whole Blood",
        _ => null,
    };

    /// <summary>
    /// <c>Personal.GenderAtBirth</c>. The biobank records a sex; the ontology is GSSO, which spells
    /// it as an assignment at birth.
    /// </summary>
    public static string? GenderAtBirth(string? sex) => sex?.ToLowerInvariant() switch
    {
        "male" => "assigned male at birth",
        "female" => "assigned female at birth",
        _ => null,
    };

    /// <summary>
    /// <c>Biospecimens.Availability</c>, from the count of units still on the shelf. The export
    /// distinguishes only "some left" from "none left", so Reserved and Destroyed never apply.
    /// </summary>
    public static string? Availability(int? availableSamples) => availableSamples switch
    {
        null => null,
        0 => "Depleted",
        _ => "Available",
    };

    private static readonly FrozenDictionary<string, string> Institutes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BBM"] = "Bank of Biological Material, Masaryk Memorial Cancer Institute",
            ["MOU"] = "Masaryk Memorial Cancer Institute",
            ["MMCI"] = "Masaryk Memorial Cancer Institute",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <c>Personal.PrimaryAffiliatedInstitute</c> and <c>Biospecimens.ManagingBiobank</c>, which
    /// share one institute list. An unrecognised biobank code is left out rather than guessed at.
    /// </summary>
    public static string? Institute(string? biobank) =>
        biobank is not null && Institutes.TryGetValue(biobank, out var name) ? name : null;

    private static readonly FrozenDictionary<string, string> Platforms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["illumina"] = "Illumina platform",
            ["ion torrent"] = "Ion Torrent platform",
            ["pacbio"] = "PacBio platform",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary><c>Sequencing.SequencingPlatform</c>: the ontology suffixes every vendor.</summary>
    public static string? SequencingPlatform(string? platform) =>
        platform is not null && Platforms.TryGetValue(platform.Trim(), out var name) ? name : null;

    private static readonly FrozenDictionary<string, string> FileFormats =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["fastq"] = "FASTQ",
            ["bam"] = "BAM",
            ["bam_index"] = "BAI",
            ["vcf"] = "VCF",
            ["vcf_filtered"] = "VCF",
            ["variant_report"] = "PDF",
            ["coverage_report"] = "PDF",
            ["summary_report"] = "PDF",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <c>Analysis.DataFormatsStored</c>, from the sequencing API's file roles. A role names what
    /// the file is for; the ontology names what it is encoded as, so several roles collapse onto
    /// one format and <c>other</c> maps to nothing.
    /// </summary>
    public static IReadOnlyList<string> DataFormats(IEnumerable<string?> roles) =>
    [
        .. roles
            .Where(role => role is not null)
            .Select(role => FileFormats.TryGetValue(role!, out var format) ? format : null)
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal),
    ];

    /// <summary>
    /// <c>Analysis.ReferenceGenomeUsed</c>. The ontology carries the bare builds and their patch
    /// releases, but the pipelines write aliases (<c>hg19</c>, <c>hg38</c>) that are not terms.
    /// </summary>
    public static string? ReferenceGenome(string? genome) => genome?.Trim().ToLowerInvariant() switch
    {
        "grch37" or "hg19" => "GRCh37",
        "grch38" or "hg38" => "GRCh38",
        _ => null,
    };
}
