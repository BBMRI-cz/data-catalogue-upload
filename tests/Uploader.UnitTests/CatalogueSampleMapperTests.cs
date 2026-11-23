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

    private static CatalogueSamplePayload Payload(SampleDto dto, string? biobank = null) =>
        CatalogueSampleMapper.ToPayload(dto, SamplePseudonym, PatientPseudonym, biobank);

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
            .ToPayload(new PatientDto { PatientId = "271801", Samples = [Sample()] }, PatientPseudonym, "study")
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
}
