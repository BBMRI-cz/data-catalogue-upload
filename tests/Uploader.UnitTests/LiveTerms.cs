using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;

namespace Uploader.UnitTests;

/// <summary>
/// Small hand-written stand-ins for the catalogue's live term lists. They hold every term a test
/// expects to see published, so a value outside them is a miss exactly as it would be against the real
/// catalogue.
/// </summary>
internal static class LiveTerms
{
    public static readonly string[] GendersAtBirth = ["assigned male at birth", "assigned female at birth"];

    public static readonly string[] Institutes =
    [
        "Masaryk Memorial Cancer Institute",
        "Bank of Biological Material, Masaryk Memorial Cancer Institute",
    ];

    /// <summary>
    /// <c>C99.9</c> is here so a genome sample's diagnosis is left out because the mapper excludes it,
    /// not because it happens not to be a term.
    /// </summary>
    public static readonly string[] Diagnoses = ["C50", "C50.4", "C51", "C77.7", "C99.9"];

    public static readonly string[] MaterialTypes = ["Solid Tissue Specimen", "Peripheral Blood"];

    public static readonly string[] PathologicalStates = ["Tumor", "Normal"];

    public static readonly string[] BiospecimenForms = ["Frozen Tissue", "Serum or Plasma", "Blood DNA", "Whole Blood"];

    public static readonly string[] StorageConditions =
    [
        "Cryotube 1–2mL Programmable freezing to <-135°C",
        "PP tube 0.5–2mL (-35) to (-18)°C",
    ];

    public static readonly string[] Availabilities = ["Available", "Depleted"];

    /// <summary>
    /// Fixture spellings: no rule translates a kit, so a kit is published only when the source already
    /// spells it as a term, and a test's kit has to be in here to be.
    /// </summary>
    public static readonly string[] LibraryPreparationKits = ["KitA", "KAPA HyperPlus"];

    public static readonly string[] TargetEnrichmentKits = ["Enrich", "KAPA HyperCap"];

    /// <summary>HGNC symbols, without the star; both gene lists read the same ontology.</summary>
    public static readonly string[] Genes = ["ALK", "BRCA1", "BRCA2", "EGFR", "TP53"];

    public static readonly string[] Platforms = ["Illumina platform", "Ion Torrent platform", "PacBio platform"];

    public static readonly string[] InstrumentModels = ["Illumina NextSeq 550", "NextSeq 500", "MiSeq"];

    public static readonly string[] SequencingMethods = ["Illumina Sequencing"];

    public static readonly string[] DataFormats = ["BAI", "BAM", "FASTQ", "PDF", "VCF"];

    public static readonly string[] ReferenceGenomes = ["GRCh37", "GRCh38"];

    /// <summary>The lists above, with <paramref name="overrides"/> replacing a field's list.</summary>
    public static TermLists Lists(params (OntologyField Field, string[] Terms)[] overrides)
    {
        var lists = OntologyFields.All.ToDictionary(field => field, _ => (IReadOnlySet<string>)new HashSet<string>());
        lists[OntologyFields.GenderAtBirth] = new HashSet<string>(GendersAtBirth);
        lists[OntologyFields.PrimaryAffiliatedInstitute] = new HashSet<string>(Institutes);
        lists[OntologyFields.Diagnosis] = new HashSet<string>(Diagnoses);
        lists[OntologyFields.MaterialType] = new HashSet<string>(MaterialTypes);
        lists[OntologyFields.PathologicalState] = new HashSet<string>(PathologicalStates);
        lists[OntologyFields.BiospecimenForm] = new HashSet<string>(BiospecimenForms);
        lists[OntologyFields.StorageConditions] = new HashSet<string>(StorageConditions);
        lists[OntologyFields.Availability] = new HashSet<string>(Availabilities);
        lists[OntologyFields.ManagingBiobank] = new HashSet<string>(Institutes);
        lists[OntologyFields.LibraryPreparationKit] = new HashSet<string>(LibraryPreparationKits);
        lists[OntologyFields.TargetEnrichmentKit] = new HashSet<string>(TargetEnrichmentKits);
        lists[OntologyFields.FullySequencedGenes] = new HashSet<string>(Genes);
        lists[OntologyFields.PartiallySequencedGenes] = new HashSet<string>(Genes);
        lists[OntologyFields.SequencingPlatform] = new HashSet<string>(Platforms);
        lists[OntologyFields.SequencingInstrumentModel] = new HashSet<string>(InstrumentModels);
        lists[OntologyFields.SequencingMethod] = new HashSet<string>(SequencingMethods);
        lists[OntologyFields.DataFormatsStored] = new HashSet<string>(DataFormats);
        lists[OntologyFields.ReferenceGenomeUsed] = new HashSet<string>(ReferenceGenomes);

        foreach (var (field, terms) in overrides)
        {
            lists[field] = new HashSet<string>(terms);
        }

        return new TermLists(lists);
    }

    /// <summary>A real resolver over <see cref="Lists"/> and, unless told otherwise, today's rules.</summary>
    public static TermResolver Resolver(IMappingSnapshot? mapping = null) =>
        new(Lists(), mapping ?? new RuleMappingSnapshot());
}
