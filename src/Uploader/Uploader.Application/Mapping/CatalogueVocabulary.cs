using System.Collections.Frozen;

namespace Uploader.Application.Mapping;

/// <summary>
/// The sequencing API's values translated into the terms the catalogue's ontology tables actually
/// contain.
/// <para>
/// EMX2 enforces these as foreign keys: a term that is not already in the ontology rejects the
/// whole row, transactionally (see <c>docs/catalogue-api-contract.md</c>). So a value only appears
/// here when there is a term that genuinely means the same thing. Everything else maps to
/// <c>null</c> and the column is omitted - an absent value is honest, and a wrong one would be a
/// clinical claim nobody made.
/// </para>
/// <para>
/// Every term below was verified present in <c>FairGenomesTest</c> on 2026-09-08. The gateway
/// re-checks them at run time against the live ontology, so a schema that drops or renames a term
/// costs one column rather than the row.
/// </para>
/// <para>
/// The patient's and the sample's terms are no longer translated here: they go through the run's
/// <see cref="TermResolver"/>, with their rules in <see cref="RuleMappingSnapshot"/>. What is left is
/// the sequencing's, which follows in #121.
/// </para>
/// </summary>
public static class CatalogueVocabulary
{
    /// <summary>
    /// <c>Sequencing.SequencingMethod</c>. The source has no method field - the sample sheet's assay
    /// names a kit or an index set - and every MMCI run is Illumina sequencing by synthesis, so this
    /// is the constant the previous uploader sent for MiSeq and NextSeq alike.
    /// </summary>
    public const string SequencingMethod = "Illumina Sequencing";

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

    /// <summary>
    /// <c>Sequencing.SequencingInstrumentModel</c>, from the instrument's serial number. The source's
    /// own model is the folder the run was filed under (<c>MiSeq</c>, <c>NextSeq</c>), and the ontology
    /// has no bare <c>NextSeq</c>: it tells the 500 from the 550, and so does the serial prefix.
    /// </summary>
    public static string? SequencingInstrumentModel(string? instrumentId) =>
        instrumentId?.Trim().ToUpperInvariant() switch
        {
            { } id when id.StartsWith("NB55", StringComparison.Ordinal) => "Illumina NextSeq 550",
            { } id when id.StartsWith("NS500", StringComparison.Ordinal)
                || id.StartsWith("NB501", StringComparison.Ordinal) => "NextSeq 500",
            { } id when id.StartsWith("M0", StringComparison.Ordinal) => "MiSeq",
            _ => null,
        };

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
