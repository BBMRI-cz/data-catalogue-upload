using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;

namespace Uploader.UnitTests;

/// <summary>
/// Small hand-written stand-ins for the catalogue's live term lists. They hold every term a test
/// expects to see published, so a value outside them is a miss exactly as it would be against the real
/// catalogue. A field nothing resolves into yet - the sequencing's - has an empty list.
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
