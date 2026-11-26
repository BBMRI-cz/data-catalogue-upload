using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;

namespace Uploader.IntegrationTests;

/// <summary>
/// Small hand-written stand-ins for the catalogue's live term lists, holding the terms the recorded
/// biobank and sequencing responses resolve to under today's rules. The recorded NextSeq run's kits
/// (<c>TSO500 DNA</c>, <c>TSO500</c>) are deliberately not here, so a kit that is not a term is seen
/// leaving the payload.
/// </summary>
internal static class LiveTerms
{
    private static readonly string[] Institutes =
    [
        "Masaryk Memorial Cancer Institute",
        "Bank of Biological Material, Masaryk Memorial Cancer Institute",
    ];

    private static readonly string[] Genes = ["ALK", "BRCA1", "BRCA2", "EGFR", "TP53"];

    public static TermResolver Resolver()
    {
        var lists = OntologyFields.All.ToDictionary(field => field, _ => (IReadOnlySet<string>)new HashSet<string>());
        lists[OntologyFields.GenderAtBirth] = new HashSet<string> { "assigned male at birth", "assigned female at birth" };
        lists[OntologyFields.PrimaryAffiliatedInstitute] = new HashSet<string>(Institutes);
        lists[OntologyFields.Diagnosis] = new HashSet<string> { "C50.4", "C77.7" };
        lists[OntologyFields.MaterialType] = new HashSet<string> { "Solid Tissue Specimen", "Peripheral Blood" };
        lists[OntologyFields.PathologicalState] = new HashSet<string> { "Tumor", "Normal" };
        lists[OntologyFields.BiospecimenForm] = new HashSet<string>
        {
            "Frozen Tissue",
            "Serum or Plasma",
            "Blood DNA",
            "Whole Blood",
        };
        lists[OntologyFields.StorageConditions] = new HashSet<string>
        {
            "Cryotube 1–2mL Programmable freezing to <-135°C",
            "PP tube 0.5–2mL (-35) to (-18)°C",
        };
        lists[OntologyFields.Availability] = new HashSet<string> { "Available", "Depleted" };
        lists[OntologyFields.ManagingBiobank] = new HashSet<string>(Institutes);
        lists[OntologyFields.LibraryPreparationKit] = new HashSet<string> { "KAPA HyperPlus" };
        lists[OntologyFields.TargetEnrichmentKit] = new HashSet<string> { "KAPA HyperCap" };
        lists[OntologyFields.FullySequencedGenes] = new HashSet<string>(Genes);
        lists[OntologyFields.PartiallySequencedGenes] = new HashSet<string>(Genes);
        lists[OntologyFields.SequencingPlatform] = new HashSet<string> { "Illumina platform" };
        lists[OntologyFields.SequencingInstrumentModel] = new HashSet<string> { "MiSeq", "Illumina NextSeq 550" };
        lists[OntologyFields.SequencingMethod] = new HashSet<string> { "Illumina Sequencing" };
        lists[OntologyFields.DataFormatsStored] = new HashSet<string> { "BAI", "BAM", "PDF", "VCF" };
        lists[OntologyFields.ReferenceGenomeUsed] = new HashSet<string> { "GRCh37" };
        return new TermResolver(new TermLists(lists), new RuleMappingSnapshot());
    }
}
