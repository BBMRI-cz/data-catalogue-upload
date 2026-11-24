using Uploader.Application.Abstractions;
using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The biobank's sample record straight to the catalogue's <c>Material</c> and <c>Biospecimens</c>
/// rows. The pseudonyms are the fake map's <c>mmci_&lt;kind&gt;_&lt;id&gt;</c>, so an assertion states
/// both that a field was substituted and which identifier it was substituted from.
/// </summary>
public sealed class CatalogueSampleMapperTests
{
    private const string PatientPseudonym = "mmci_patient_P1";
    private const string SamplePseudonym = "mmci_sample_S1";
    private const string BiospecimenPseudonym = "mmci_biospecimen_S1";

    private static SampleDto Sample() => new()
    {
        SampleId = "BBMs:2022:3249:SD",
        Type = "serum",
        MaterialType = "SD",
        PredictiveNumber = "4-21",
        AvailableSamplesNo = 2,
        TakingDate = new DateTime(2022, 12, 7),
    };

    private static CatalogueSamplePayload Payload(
        SampleDto dto,
        string? biobank = null,
        List<UnmappedValue>? misses = null,
        TermResolver? terms = null) =>
        CatalogueSampleMapper.ToPayload(
            dto, SamplePseudonym, PatientPseudonym, biobank, terms ?? LiveTerms.Resolver(), misses ?? []);

    [Fact]
    public void MaterialCarriesBothPseudonymsAndTheDerivedDiagnosisReference()
    {
        var payload = Payload(Sample());

        Assert.Equal(SamplePseudonym, payload.ExternalId);
        Assert.Equal(PatientPseudonym, payload.PatientId);
        Assert.Equal(SamplePseudonym, payload.Material!.MaterialIdentifier);
        Assert.Equal(PatientPseudonym, payload.Material.CollectedFromPerson);
        Assert.Equal("2022-12-07", payload.Material.SamplingDate);
    }

    /// <summary>
    /// Material and biospecimen are two rows for one archived sample, so they cannot share a key.
    /// The biospecimen derives its own from the sample pseudonym and points back at the material.
    /// </summary>
    [Fact]
    public void BiospecimenDerivesItsKeyAndReferencesTheMaterial()
    {
        var payload = Payload(Sample());

        Assert.Equal(BiospecimenPseudonym, payload.Biospecimen!.BiospecimenIdentifier);
        Assert.Equal(SamplePseudonym, payload.Biospecimen.DerivedFromMaterial);
        Assert.Equal("Serum or Plasma", payload.Biospecimen.BiospecimenForm);
        Assert.Equal(2, payload.Biospecimen.Quantity);
    }

    /// <summary>
    /// A reference stores the referenced row's key, so the two have to be produced the same way. If
    /// they ever drift the catalogue's graph breaks without anything failing.
    /// </summary>
    [Fact]
    public void TheDiagnosisReferenceEqualsTheClinicalKeyItPointsAt()
    {
        var clinicalKey = CataloguePatientMapper
            .ToPayload(
                new PatientDto { PatientId = "271801", Samples = [Sample()] },
                PatientPseudonym,
                "study",
                LiveTerms.Resolver(),
                [])
            .Clinical!.ClinicalIdentifier!;
        var reference = Payload(Sample()).Material!.BelongsToDiagnosis;

        Assert.Equal([clinicalKey], reference);
    }

    [Fact]
    public void MapsMaterialAndBiospecimen()
    {
        var payload = Payload(new SampleDto { SampleId = "S1", Type = "tissue", MaterialType = "1" }, biobank: "MOU");

        Assert.Equal(SamplePseudonym, payload.Material!.MaterialIdentifier);
        Assert.Equal("Solid Tissue Specimen", payload.Material.MaterialType);
        Assert.Equal("Masaryk Memorial Cancer Institute", payload.Biospecimen!.ManagingBiobank);
    }

    [Fact]
    public void TissueIsDatedByItsCutWhileOthersAreDatedByTheirTakingDate()
    {
        var tissue = Payload(new SampleDto
        {
            SampleId = "S1",
            Type = "tissue",
            CutTime = new DateTime(2020, 1, 2, 3, 4, 5),
            FreezeTime = new DateTime(2020, 1, 2, 4, 0, 0),
            TakingDate = new DateTime(1999, 1, 1),
        });

        var serum = Payload(
            new SampleDto { SampleId = "S2", Type = "serum", TakingDate = new DateTime(2021, 5, 6, 7, 8, 9) });

        // v2 keeps one date where v1 had a sampling and a registration timestamp, so the freeze
        // time has nowhere left to go.
        Assert.Equal("2020-01-02", tissue.Material!.SamplingDate);
        Assert.Equal("2021-05-06", serum.Material!.SamplingDate);
    }

    /// <summary>
    /// The planner's side of the same record: the real ids sync state is keyed on, and the links
    /// that decide which sequencing and WSI the sample owns.
    /// </summary>
    [Fact]
    public void SampleCarriesTypedReferences()
    {
        var sample = CatalogueSampleMapper.ToSample(
            new SampleDto { SampleId = "S1", PredictiveNumber = "PRED1", Biopsy = "2023/2872-1" },
            new PatientId("P1")).Value;

        Assert.Equal(new SampleId("S1"), sample.Id);
        Assert.Equal(new PatientId("P1"), sample.PatientId);
        Assert.Equal(new SequencingId("PRED1"), sample.SequencingId);

        // The biobank serves no bioptic number; the WSI link waits for #31.
        Assert.Null(sample.WsiId);
    }

    /// <summary>
    /// The material code reaches the resolver as the biobank served it, with the sample type and the
    /// biobank API's label as its context. A code no rule has decided on for a column - here genome
    /// <c>PS</c>'s tube, which the previous uploader never named - is left out of a row that still
    /// goes out, and recorded with that context, which is what an entry would be keyed on.
    /// </summary>
    [Fact]
    public void ACodeNoRuleHasDecidedOnIsLeftOutAndRecordedWithItsContext()
    {
        var misses = new List<UnmappedValue>();

        var payload = Payload(
            new SampleDto { SampleId = "S1", Type = "genome", MaterialType = "PS", MaterialTypeLabel = "Whole blood with DNA stabiliser" },
            misses: misses);

        Assert.Equal("Peripheral Blood", payload.Material!.MaterialType);
        Assert.Equal("Whole Blood", payload.Biospecimen!.BiospecimenForm);
        Assert.Null(payload.Biospecimen.StorageConditions);
        Assert.Equal(
            [
                new UnmappedValue(
                    OntologyFields.StorageConditions,
                    "PS",
                    TermContext.Material("genome", "Whole blood with DNA stabiliser")),
            ],
            misses);
    }

    /// <summary>
    /// Benign and premalignant tissue, mononuclear cells, primary cell cultures and RNAlater storage
    /// have no term on purpose, so nothing is reported - the unmapped list is for values nobody has
    /// decided on. Which columns each of them leaves empty is pinned in <see cref="RuleMappingTableTests"/>.
    /// </summary>
    [Theory]
    [InlineData("tissue", "3")]    // benign: no pathological state
    [InlineData("tissue", "56")]   // benign in RNAlater: no pathological state, no tube
    [InlineData("tissue", "5")]    // premalignant
    [InlineData("tissue", "7")]    // mononuclear cells: no state, no listed form
    [InlineData("serum", "PR")]    // primary cell culture: no material type, no listed form
    public void TheDeliberateEmptiesAreNoMiss(string type, string code)
    {
        var misses = new List<UnmappedValue>();

        Payload(new SampleDto { SampleId = "S1", Type = type, MaterialType = code }, biobank: "MOU", misses: misses);

        Assert.Empty(misses);
    }

    /// <summary>Not guessed at, and no longer silently dropped: the managing biobank goes on the list.</summary>
    [Fact]
    public void AnUnknownBiobankCodeIsLeftOutAndRecorded()
    {
        var misses = new List<UnmappedValue>();

        var payload = Payload(Sample(), biobank: "XYZ", misses: misses);

        Assert.Null(payload.Biospecimen!.ManagingBiobank);
        Assert.Equal("Serum or Plasma", payload.Biospecimen.BiospecimenForm);
        Assert.Equal([new UnmappedValue(OntologyFields.ManagingBiobank, "XYZ", TermContext.Empty)], misses);
    }

    [Theory]
    [InlineData(0, "Depleted")]
    [InlineData(1, "Available")]
    [InlineData(null, null)]    // the export did not say, so neither do we
    public void AvailabilityIsWorkedOutFromTheUnitsLeft(int? unitsLeft, string? term) =>
        Assert.Equal(term, Payload(Sample() with { AvailableSamplesNo = unitsLeft }).Biospecimen!.Availability);

    /// <summary>
    /// Worked out in our code rather than looked up, but still only sent as a live term: a catalogue
    /// that drops <c>Depleted</c> shows it on the unmapped list instead of rejecting the row.
    /// </summary>
    [Fact]
    public void AnAvailabilityTheCatalogueDoesNotListIsAMiss()
    {
        var misses = new List<UnmappedValue>();
        var terms = new TermResolver(
            LiveTerms.Lists((OntologyFields.Availability, ["Available"])), new RuleMappingSnapshot());

        var payload = Payload(Sample() with { AvailableSamplesNo = 0 }, biobank: "MOU", misses: misses, terms: terms);

        Assert.Null(payload.Biospecimen!.Availability);
        Assert.Equal(0, payload.Biospecimen.Quantity);
        Assert.Equal([new UnmappedValue(OntologyFields.Availability, "Depleted", TermContext.Empty)], misses);
    }

    /// <summary>
    /// A code the source did not fill is absent, not unmapped: all four material columns stay empty
    /// and nothing is reported. The biobank API rejects a sample without one, so this guards the
    /// resolver's contract rather than a case the export produces.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData(" ")]
    public void AnAbsentMaterialCodeFillsNoColumnAndIsNoMiss(string? code)
    {
        var misses = new List<UnmappedValue>();

        var payload = Payload(new SampleDto { SampleId = "S1", Type = "tissue", MaterialType = code }, misses: misses);

        Assert.Null(payload.Material!.MaterialType);
        Assert.Null(payload.Material.PathologicalState);
        Assert.Null(payload.Biospecimen!.BiospecimenForm);
        Assert.Null(payload.Biospecimen.StorageConditions);
        Assert.Empty(misses);
    }
}

