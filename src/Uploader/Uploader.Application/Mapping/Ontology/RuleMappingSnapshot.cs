using System.Collections.Frozen;
using Uploader.Application.Abstractions;

namespace Uploader.Application.Mapping;

/// <summary>
/// The stand-in mapping table's rules: the hand-written ones the uploader has always translated source
/// values with, answering through the snapshot port the ontology-mapping student's approved table will.
/// <see cref="RuleMappingTable"/> hands it to every run.
/// <para>
/// A rule answers a term when it knows one, and not found for anything else, so a value nobody has
/// seen before - a new spelling of a sex, an unknown biobank code - lands on the unmapped list instead
/// of vanishing. A field with no rule is not found either. Every term below was verified present in
/// <c>FairGenomesTest</c> on 2026-09-08, and the resolver checks each answer against the live list
/// anyway, so a term the catalogue drops shows up as unmapped rather than failing the row.
/// </para>
/// </summary>
public sealed class RuleMappingSnapshot : IMappingSnapshot
{
    private static readonly FrozenDictionary<OntologyField, Func<string, MappingAnswer>> Rules =
        new Dictionary<OntologyField, Func<string, MappingAnswer>>
        {
            [OntologyFields.GenderAtBirth] = GenderAtBirth,
            [OntologyFields.PrimaryAffiliatedInstitute] = code => Found(Institute(code)),
            [OntologyFields.Diagnosis] = Diagnosis,
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, string> Institutes =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["BBM"] = "Bank of Biological Material, Masaryk Memorial Cancer Institute",
            ["MOU"] = "Masaryk Memorial Cancer Institute",
            ["MMCI"] = "Masaryk Memorial Cancer Institute",
        }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);

    public MappingAnswer Lookup(OntologyField field, string value, TermContext context) =>
        Rules.TryGetValue(field, out var rule) ? rule(value) : MappingAnswer.NotFound;

    /// <summary>
    /// <c>Personal.PrimaryAffiliatedInstitute</c> and <c>Biospecimens.ManagingBiobank</c> share one
    /// institute list, keyed on the biobank's code. An unrecognised code has no institute rather than
    /// a guessed one.
    /// </summary>
    internal static string? Institute(string? biobank) =>
        biobank is not null && Institutes.TryGetValue(biobank, out var name) ? name : null;

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

    private static MappingAnswer Found(string? term) =>
        term is null ? MappingAnswer.NotFound : MappingAnswer.Mapped(term);
}
