using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;

namespace Uploader.IntegrationTests;

/// <summary>
/// Small hand-written stand-ins for the catalogue's live term lists, holding the patient terms the
/// recorded biobank response resolves to under today's rules. A field the patient does not fill has an
/// empty list.
/// </summary>
internal static class LiveTerms
{
    public static TermResolver Resolver()
    {
        var lists = OntologyFields.All.ToDictionary(field => field, _ => (IReadOnlySet<string>)new HashSet<string>());
        lists[OntologyFields.GenderAtBirth] = new HashSet<string> { "assigned male at birth", "assigned female at birth" };
        lists[OntologyFields.PrimaryAffiliatedInstitute] = new HashSet<string>
        {
            "Masaryk Memorial Cancer Institute",
            "Bank of Biological Material, Masaryk Memorial Cancer Institute",
        };
        lists[OntologyFields.Diagnosis] = new HashSet<string> { "C50.4", "C77.7" };
        return new TermResolver(new TermLists(lists), new RuleMappingSnapshot());
    }
}
