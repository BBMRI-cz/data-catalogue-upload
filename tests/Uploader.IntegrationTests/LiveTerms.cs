using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;

namespace Uploader.IntegrationTests;

/// <summary>
/// Small hand-written stand-ins for the catalogue's live term lists, holding the patient and sample
/// terms the recorded biobank response resolves to under today's rules. A field nothing resolves into
/// yet - the sequencing's - has an empty list.
/// </summary>
internal static class LiveTerms
{
    private static readonly string[] Institutes =
    [
        "Masaryk Memorial Cancer Institute",
        "Bank of Biological Material, Masaryk Memorial Cancer Institute",
    ];

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
        return new TermResolver(new TermLists(lists), new RuleMappingSnapshot());
    }
}
