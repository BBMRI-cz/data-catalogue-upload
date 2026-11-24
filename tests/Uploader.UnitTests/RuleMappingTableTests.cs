using Uploader.Application.Abstractions;
using Uploader.Application.Mapping;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The stand-in for the mapping table: today's hand-written rules, behind the port the ontology-mapping
/// student's approved table will plug into. Every rule answers one of three ways - a term, "no term" for
/// a value deliberately left empty, or not found for a value nobody has decided on - and only the last
/// puts a value on the unmapped list. A term is a foreign key in EMX2, so the expected strings match it
/// character for character.
/// </summary>
public sealed class RuleMappingTableTests
{
    private static readonly IMappingSnapshot Rules = new RuleMappingSnapshot();

    private static MappingAnswer Lookup(OntologyField field, string value) =>
        Rules.Lookup(field, value, TermContext.Empty);

    [Theory]
    [InlineData("male", "assigned male at birth")]
    [InlineData("female", "assigned female at birth")]
    [InlineData("Female", "assigned female at birth")]   // a term must not hinge on the source's casing
    public void SexBecomesTheGssoAssignmentAtBirth(string sex, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.GenderAtBirth, sex));

    [Theory]
    [InlineData("unknown")]
    [InlineData("x")]
    public void ASexNoRuleKnowsIsNotFound(string sex) =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(OntologyFields.GenderAtBirth, sex));

    [Theory]
    [InlineData("MOU", "Masaryk Memorial Cancer Institute")]
    [InlineData("MMCI", "Masaryk Memorial Cancer Institute")]
    [InlineData("mou", "Masaryk Memorial Cancer Institute")]
    [InlineData("BBM", "Bank of Biological Material, Masaryk Memorial Cancer Institute")]
    public void ABiobankCodeBecomesItsInstitute(string code, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.PrimaryAffiliatedInstitute, code));

    /// <summary>Not guessed at, and no longer silently dropped: it lands on the unmapped list.</summary>
    [Fact]
    public void AnUnknownBiobankCodeIsNotFound() =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(OntologyFields.PrimaryAffiliatedInstitute, "XYZ"));

    /// <summary>The biobank exports ICD-10 without the dot; the Diagnosis ontology spells it out.</summary>
    [Theory]
    [InlineData("C504", "C50.4")]      // four characters: the last one is the subcategory
    [InlineData(" C774 ", "C77.4")]    // trimmed first
    [InlineData(" C50 ", "C50")]       // trimmed, with no subcategory to separate
    public void DiagnosisGetsTheIcd10Dot(string code, string term) =>
        Assert.Equal(MappingAnswer.Mapped(term), Lookup(OntologyFields.Diagnosis, code));

    /// <summary>
    /// A code the rule would hand back unchanged has nothing translated: if it is a term the resolver
    /// already used it as one, and if it is not, saying otherwise would only hide it.
    /// </summary>
    [Theory]
    [InlineData("C50")]      // already the ontology's spelling
    [InlineData("C5091")]    // five characters: no rule says where the dot goes
    public void ADiagnosisTheDotRuleLeavesAloneIsNotFound(string code) =>
        Assert.Equal(MappingAnswer.NotFound, Lookup(OntologyFields.Diagnosis, code));

    /// <summary>Loading does nothing: every run is handed the rules' one snapshot.</summary>
    [Fact]
    public async Task LoadingHandsBackTheRules()
    {
        var loaded = await new RuleMappingTable().LoadAsync(CancellationToken.None);

        Assert.Equal(
            MappingAnswer.Mapped("C50.4"),
            loaded.Value.Lookup(OntologyFields.Diagnosis, "C504", TermContext.Empty));
    }
}
