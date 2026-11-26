using System.Collections.Frozen;
using Uploader.Application.Abstractions;

namespace Uploader.Application.Mapping;

/// <summary>
/// The stand-in mapping table's rules: the hand-written ones the uploader has always translated source
/// values with, answering through the snapshot port the ontology-mapping student's approved table will.
/// <see cref="RuleMappingTable"/> hands it to every run.
/// <para>
/// A rule answers a term when it knows one; "no term" where leaving the field empty is a documented
/// decision (benign and premalignant tissue, mononuclear cells, primary cell cultures, RNAlater
/// storage, a blood draw's pathological state, the <c>other</c> file role, a stray gene star); and
/// not found for anything else, so a value nobody has seen before - a new material code, an unknown
/// biobank code, a NovaSeq serial - lands on the unmapped list instead of vanishing. A field with no
/// rule is not found either: the kits, which have no code list to translate from and are published
/// only when the source already spells them as a term, and the sequencing method, which our code
/// computes. Every term below was verified
/// present in <c>FairGenomesTest</c> on 2026-09-08, and the resolver checks each answer against the
/// live list anyway, so a term the catalogue drops shows up as unmapped rather than failing the row.
/// </para>
/// <para>
/// The four material-code fields read the code's sample type from its <see cref="TermContext"/>: the
/// same code is a different material on a tissue and on a serum sample. Today's rules ignore the
/// biobank API's label; it is in the context for the student's table.
/// </para>
/// </summary>
public sealed class RuleMappingSnapshot : IMappingSnapshot
{
    private static readonly FrozenDictionary<OntologyField, Func<string, TermContext, MappingAnswer>> Rules =
        new Dictionary<OntologyField, Func<string, TermContext, MappingAnswer>>
        {
            [OntologyFields.GenderAtBirth] = (sex, _) => GenderAtBirth(sex),
            [OntologyFields.PrimaryAffiliatedInstitute] = (code, _) => Found(Institute(code)),
            [OntologyFields.Diagnosis] = (code, _) => Diagnosis(code),
            [OntologyFields.MaterialType] = (code, context) => MaterialType(context.SampleType, code),
            [OntologyFields.PathologicalState] = (code, context) => PathologicalState(context.SampleType, code),
            [OntologyFields.BiospecimenForm] = (code, context) => BiospecimenForm(context.SampleType, code),
            [OntologyFields.StorageConditions] = (code, context) => StorageConditions(context.SampleType, code),
            [OntologyFields.ManagingBiobank] = (code, _) => Found(Institute(code)),
            [OntologyFields.FullySequencedGenes] = (gene, _) => Gene(gene),
            [OntologyFields.PartiallySequencedGenes] = (gene, _) => Gene(gene),
            [OntologyFields.SequencingPlatform] = (platform, _) => Found(Platform(platform)),
            [OntologyFields.SequencingInstrumentModel] = (serial, _) => Found(InstrumentModel(serial)),
            [OntologyFields.DataFormatsStored] = (role, _) => DataFormat(role),
            [OntologyFields.ReferenceGenomeUsed] = (alias, _) => Found(ReferenceGenome(alias)),
        }.ToFrozenDictionary();

    public MappingAnswer Lookup(OntologyField field, string value, TermContext context) =>
        Rules.TryGetValue(field, out var rule) ? rule(value, context) : MappingAnswer.NotFound;

    private static readonly FrozenDictionary<string, string> Institutes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BBM"] = "Bank of Biological Material, Masaryk Memorial Cancer Institute",
            ["MOU"] = "Masaryk Memorial Cancer Institute",
            ["MMCI"] = "Masaryk Memorial Cancer Institute",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// <c>Personal.PrimaryAffiliatedInstitute</c> and <c>Biospecimens.ManagingBiobank</c> share one
    /// institute list, keyed on the biobank's code. An unrecognised code has no institute rather than
    /// a guessed one.
    /// </summary>
    private static string? Institute(string biobank) =>
        Institutes.TryGetValue(biobank, out var name) ? name : null;

    /// <summary>
    /// <c>Personal.GenderAtBirth</c>. The biobank records a sex; the ontology is GSSO, which spells it
    /// as an assignment at birth.
    /// </summary>
    private static MappingAnswer GenderAtBirth(string sex) => sex.ToLowerInvariant() switch
    {
        "male" => MappingAnswer.Mapped("assigned male at birth"),
        "female" => MappingAnswer.Mapped("assigned female at birth"),
        _ => MappingAnswer.NotFound,
    };

    /// <summary>
    /// <c>Clinical.Diagnosis</c>, by the ICD-10 dot rule: the biobank exports codes without the dot
    /// (<c>C504</c>) and the Diagnosis ontology spells the subcategory out (<c>C50.4</c>). Only a
    /// four-character code has a subcategory to separate. A code the rule would hand back unchanged is
    /// not found - the resolver has already tried it as a term, and the rule has nothing to add.
    /// </summary>
    private static MappingAnswer Diagnosis(string code)
    {
        var trimmed = code.Trim();
        var term = trimmed.Length == 4 ? $"{trimmed[..3]}.{trimmed[3]}" : trimmed;
        return term.Length == 0 || term == code ? MappingAnswer.NotFound : MappingAnswer.Mapped(term);
    }

    private const string SolidTissue = "Solid Tissue Specimen";
    private const string PeripheralBlood = "Peripheral Blood";

    /// <summary>
    /// <c>Material.MaterialType</c>: what was taken from the patient, before any processing.
    /// Tissue codes are all surgical tissue; every blood-derived code is a blood draw, and which
    /// fraction was kept is <c>Biospecimens.biospecimenForm</c>'s business, not this column's.
    /// </summary>
    private static MappingAnswer MaterialType(string? sampleType, string code) => (sampleType, code) switch
    {
        (BiobankMapping.TissueType, "7") => MappingAnswer.Mapped(PeripheralBlood), // mononuclear cells, drawn not excised
        (BiobankMapping.TissueType, _) => MappingAnswer.Mapped(SolidTissue),

        // A primary cell culture is grown, not collected, and has no term; the rest are blood.
        (BiobankMapping.SerumType, "PR") => MappingAnswer.NoTerm,
        (BiobankMapping.SerumType, _) => MappingAnswer.Mapped(PeripheralBlood),
        (BiobankMapping.GenomeType, _) => MappingAnswer.Mapped(PeripheralBlood),
        _ => MappingAnswer.NotFound,
    };

    /// <summary>
    /// <c>Material.PathologicalState</c>. The ontology offers only Normal, Tumor, Organoid and
    /// Tumoroid, so benign and premalignant tissue have nowhere to go: calling either of them
    /// "Tumor" would assert a malignancy the biobank did not record. A blood draw records no
    /// pathological state, so only tissue has one.
    /// </summary>
    private static MappingAnswer PathologicalState(string? sampleType, string code) => sampleType switch
    {
        BiobankMapping.TissueType => code switch
        {
            "1" or "53" => MappingAnswer.Mapped("Tumor"),   // malignant tumour, plain and RNAlater
            "2" or "55" => MappingAnswer.Mapped("Tumor"),   // metastasis
            "4" or "54" => MappingAnswer.Mapped("Normal"),  // healthy tissue
            "3" or "56" or "5" or "7" => MappingAnswer.NoTerm, // benign, premalignant, PBMNC
            _ => MappingAnswer.NotFound,
        },
        BiobankMapping.SerumType or BiobankMapping.GenomeType => MappingAnswer.NoTerm,
        _ => MappingAnswer.NotFound,
    };

    /// <summary>
    /// <c>Biospecimens.BiospecimenForm</c>: the form the biobank actually stores.
    /// <para>
    /// Tissue is frozen - the export carries a freeze time for every tissue sample, which is the
    /// evidence for it. The plasma and serum codes differ in anticoagulant, which the ontology does
    /// not distinguish below "Serum or Plasma".
    /// </para>
    /// </summary>
    private static MappingAnswer BiospecimenForm(string? sampleType, string code) => (sampleType, code) switch
    {
        (BiobankMapping.TissueType, "7") => MappingAnswer.NoTerm, // mononuclear cells are not a listed form
        (BiobankMapping.TissueType, _) => MappingAnswer.Mapped("Frozen Tissue"),

        (BiobankMapping.SerumType, "PR") => MappingAnswer.NoTerm, // primary cell cultures
        (BiobankMapping.SerumType, _) => MappingAnswer.Mapped("Serum or Plasma"),

        (BiobankMapping.GenomeType, "gD") => MappingAnswer.Mapped("Blood DNA"),
        (BiobankMapping.GenomeType, _) => MappingAnswer.Mapped("Whole Blood"),
        _ => MappingAnswer.NotFound,
    };

    private const string Cryotube = "Cryotube 1–2mL Programmable freezing to <-135°C";
    private const string PpTube = "PP tube 0.5–2mL (-35) to (-18)°C";

    /// <summary>
    /// <c>Biospecimens.StorageConditions</c>. The ontology names a tube and a temperature together
    /// and the export names neither, so this is the previous uploader's mapping from the material
    /// code, taken over as it was (<c>miseq_sample_metadata_collector.py</c>). The terms carry an en
    /// dash (<c>1–2mL</c>), not a hyphen.
    /// </summary>
    private static MappingAnswer StorageConditions(string? sampleType, string code) => (sampleType, code) switch
    {
        (BiobankMapping.TissueType, "1" or "2" or "3" or "4" or "5" or "7") => MappingAnswer.Mapped(Cryotube),
        (BiobankMapping.SerumType, "C" or "K" or "L" or "PD" or "SD" or "T" or "PR") => MappingAnswer.Mapped(Cryotube),
        (BiobankMapping.GenomeType, "gD" or "PK") => MappingAnswer.Mapped(PpTube),

        // RNAlater tissue: the previous uploader deliberately named no tube for it.
        (BiobankMapping.TissueType, "53" or "54" or "55" or "56") => MappingAnswer.NoTerm,

        // Serum S/PS, genome PS and any new code: nobody has named a tube for them yet.
        _ => MappingAnswer.NotFound,
    };

    /// <summary>
    /// <c>SamplePreparation.FullySequencedGenes</c> and <c>PartiallySequencedGenes</c>, by the gene
    /// star: the libraries table marks a gene whose coding regions are all covered with a star
    /// ("Genes (*all coding regions covered)"), and the sequencing API keeps it in the symbol. Which
    /// list a gene goes to is placement, and the mapper decides it from the star; the star itself is
    /// not part of the HGNC symbol (<c>BRCA1*</c> is not a term), so it comes off here. A stray star
    /// with no symbol is not a gene, on purpose. A symbol the rule would hand back unchanged is not
    /// found - the resolver has already tried it as a term, and the rule has nothing to add.
    /// </summary>
    private static MappingAnswer Gene(string gene)
    {
        var symbol = gene.Trim().TrimEnd('*');
        if (symbol.Length == 0)
        {
            return MappingAnswer.NoTerm;
        }

        return symbol == gene ? MappingAnswer.NotFound : MappingAnswer.Mapped(symbol);
    }

    private static readonly FrozenDictionary<string, string> Platforms =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["illumina"] = "Illumina platform",
            ["ion torrent"] = "Ion Torrent platform",
            ["pacbio"] = "PacBio platform",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    /// <summary><c>Sequencing.SequencingPlatform</c>: the ontology suffixes every vendor.</summary>
    private static string? Platform(string platform) =>
        Platforms.TryGetValue(platform.Trim(), out var term) ? term : null;

    /// <summary>
    /// <c>Sequencing.SequencingInstrumentModel</c>, from the instrument's serial number. The source's
    /// own model is the folder the run was filed under (<c>MiSeq</c>, <c>NextSeq</c>), and the ontology
    /// has no bare <c>NextSeq</c>: it tells the 500 from the 550, and so does the serial prefix. A
    /// serial no prefix names - a NovaSeq's <c>A00…</c> - is not found.
    /// </summary>
    private static string? InstrumentModel(string serial) => serial.Trim().ToUpperInvariant() switch
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
    /// <c>Analysis.DataFormatsStored</c>, from the sequencing API's file roles. A role names what the
    /// file is for; the ontology names what it is encoded as, so several roles collapse onto one
    /// format. <c>other</c> is the API's escape hatch for an artifact it does not recognise, which
    /// says nothing about the encoding, so it has no format on purpose; a role this list does not
    /// know is not found.
    /// </summary>
    private static MappingAnswer DataFormat(string role) => role.Trim() switch
    {
        var trimmed when FileFormats.TryGetValue(trimmed, out var format) => MappingAnswer.Mapped(format),
        var trimmed when trimmed.Equals("other", StringComparison.OrdinalIgnoreCase) => MappingAnswer.NoTerm,
        _ => MappingAnswer.NotFound,
    };

    /// <summary>
    /// <c>Analysis.ReferenceGenomeUsed</c>. The ontology carries the bare builds and their patch
    /// releases, but the pipelines write aliases (<c>hg19</c>, <c>hg38</c>) that are not terms.
    /// </summary>
    private static string? ReferenceGenome(string alias) => alias.Trim().ToLowerInvariant() switch
    {
        "grch37" or "hg19" => "GRCh37",
        "grch38" or "hg38" => "GRCh38",
        _ => null,
    };

    private static MappingAnswer Found(string? term) =>
        term is null ? MappingAnswer.NotFound : MappingAnswer.Mapped(term);
}
