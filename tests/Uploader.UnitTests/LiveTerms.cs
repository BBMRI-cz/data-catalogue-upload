using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;

namespace Uploader.UnitTests;

/// <summary>
/// Small hand-written stand-ins for the catalogue's live term lists. They hold every term a test
/// expects to see published, so a value outside them is a miss exactly as it would be against the real
/// catalogue. A field the patient does not fill has an empty list, as a field nothing resolves into yet.
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

    /// <summary>The lists above, with <paramref name="overrides"/> replacing a field's list.</summary>
    public static TermLists Lists(params (OntologyField Field, string[] Terms)[] overrides)
    {
        var lists = OntologyFields.All.ToDictionary(field => field, _ => (IReadOnlySet<string>)new HashSet<string>());
        lists[OntologyFields.GenderAtBirth] = new HashSet<string>(GendersAtBirth);
        lists[OntologyFields.PrimaryAffiliatedInstitute] = new HashSet<string>(Institutes);
        lists[OntologyFields.Diagnosis] = new HashSet<string>(Diagnoses);

        foreach (var (field, terms) in overrides)
        {
            lists[field] = new HashSet<string>(terms);
        }

        return new TermLists(lists);
    }
}
