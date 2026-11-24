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
/// storage, a blood draw's pathological state); and not found for anything else, so a value nobody
/// has seen before - a new material code, an unknown biobank code - lands on the unmapped list
/// instead of vanishing. A field with no rule is not found either. Every term below was verified
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
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> Institutes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BBM"] = "Bank of Biological Material, Masaryk Memorial Cancer Institute",
            ["MOU"] = "Masaryk Memorial Cancer Institute",
            ["MMCI"] = "Masaryk Memorial Cancer Institute",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public MappingAnswer Lookup(OntologyField field, string value, TermContext context) =>
        Rules.TryGetValue(field, out var rule) ? rule(value, context) : MappingAnswer.NotFound;

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

    private static MappingAnswer Found(string? term) =>
        term is null ? MappingAnswer.NotFound : MappingAnswer.Mapped(term);
}
