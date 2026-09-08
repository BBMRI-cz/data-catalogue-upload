using Uploader.Application.Mapping;
using Uploader.Domain;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// What the catalogue receives in place of the real identifiers. The pseudonyms here are the fake
/// map's <c>mmci_&lt;kind&gt;_&lt;id&gt;</c>, so an assertion states both that a field was substituted
/// and which identifier it was substituted from.
/// </summary>
public sealed class CatalogueMapperTests
{
    private const string PatientPseudonym = "mmci_patient_P1";
    private const string SamplePseudonym = "mmci_sample_S1";

    /// <summary>What <see cref="PatientPseudonym"/> derives to, and every reference to it must match.</summary>
    private const string ClinicalPseudonym = "mmci_clinical_P1";

    private static PatientAggregate Patient() =>
        PatientAggregate.Create(
            "271801",
            new Personal { PersonalIdentifier = "271801", YearOfBirth = 1948, GenderAtBirth = "male" },
            new Clinical
            {
                ClinicalIdentifier = "clinical_271801",
                BelongsToPerson = "271801",
                ClinicalDiagnosis = ["C50.4"],
                AgeAtDiagnosis = 74,
            },
            hasConsent: true).Value;

    private static SampleAggregate Sample() =>
        SampleAggregate.Create(
            "BBMs:2022:3249:SD",
            new PatientId("271801"),
            new SequencingId("4-21"),
            wsiId: null,
            new Material
            {
                MaterialIdentifier = "BBMs:2022:3249:SD",
                CollectedFromPerson = "271801",
                BelongsToDiagnosis = ["clinical_271801"],
                SamplingTimestamp = "2022-12-07T07:35:00",
                BiospecimenType = "SD",
                PhysicalLocation = "MOU",
            }).Value;

    private static SequencingAggregate Sequencing() =>
        SequencingAggregate.Create(
            "4-21",
            new SampleId("BBMs:2022:3249:SD"),
            [
                new SamplePreparation
                {
                    // Already pseudonymized by the source: the run tree's folder name.
                    SampleprepIdentifier = "mmci_sampleprep_abc_RUN1",
                    BelongsToMaterial = "BBMs:2022:3249:SD",
                    LibraryPreparationKit = "KAPA",
                    Sequencing = new SequencingRun
                    {
                        SequencingIdentifier = "mmci_predictive_abc_RUN1",
                        BelongsToSamplePreparation = "mmci_sampleprep_abc_RUN1",
                        SequencingPlatform = "Illumina",
                        Analyses =
                        [
                            new Analysis
                            {
                                AnalysisIdentifier = "mmci_analysis_abc_RUN1",
                                BelongsToSequencing = "mmci_predictive_abc_RUN1",
                                AbstractDataLocation = "Samples/mmci_predictive_abc/VCF/x.vcf",
                            },
                        ],
                    },
                },
            ]).Value;

    [Fact]
    public void PersonalCarriesThePatientPseudonymAsItsKey()
    {
        var payload = CatalogueMapper.ToPayload(Patient(), PatientPseudonym);

        Assert.Equal(PatientPseudonym, payload.ExternalId);
        Assert.Equal(PatientPseudonym, payload.Personal!.PersonalIdentifier);

        // Everything that is not an identifier is carried through untouched.
        Assert.Equal(1948, payload.Personal.YearOfBirth);
        Assert.Equal("male", payload.Personal.GenderAtBirth);
    }

    [Fact]
    public void ClinicalIsDerivedFromThePseudonymAndPointsBackAtIt()
    {
        var payload = CatalogueMapper.ToPayload(Patient(), PatientPseudonym);

        Assert.Equal(ClinicalPseudonym, payload.Clinical!.ClinicalIdentifier);
        Assert.Equal(PatientPseudonym, payload.Clinical.BelongsToPerson);
        Assert.Equal(["C50.4"], payload.Clinical.ClinicalDiagnosis);
        Assert.Equal(74, payload.Clinical.AgeAtDiagnosis);
    }

    [Fact]
    public void MaterialCarriesBothPseudonymsAndTheDerivedDiagnosisReference()
    {
        var payload = CatalogueMapper.ToPayload(Sample(), SamplePseudonym, PatientPseudonym);

        Assert.Equal(SamplePseudonym, payload.ExternalId);
        Assert.Equal(PatientPseudonym, payload.PatientId);
        Assert.Equal(SamplePseudonym, payload.Material!.MaterialIdentifier);
        Assert.Equal(PatientPseudonym, payload.Material.CollectedFromPerson);
        Assert.Equal("2022-12-07T07:35:00", payload.Material.SamplingTimestamp);
    }

    /// <summary>
    /// A reference stores the referenced row's key, so the two have to be produced the same way. If
    /// they ever drift the catalogue's graph breaks without anything failing.
    /// </summary>
    [Fact]
    public void TheDiagnosisReferenceEqualsTheClinicalKeyItPointsAt()
    {
        var clinicalKey = CatalogueMapper.ToPayload(Patient(), PatientPseudonym).Clinical!.ClinicalIdentifier!;
        var reference = CatalogueMapper.ToPayload(Sample(), SamplePseudonym, PatientPseudonym)
            .Material!.BelongsToDiagnosis;

        Assert.Equal([clinicalKey], reference);
    }

    /// <summary>
    /// It references a different sample's material, so this sample's pseudonym is the wrong answer
    /// and the real id is worse. Nothing sets it today; it is dropped rather than forwarded.
    /// </summary>
    [Fact]
    public void DerivedFromIsDroppedRatherThanForwarded()
    {
        var sample = SampleAggregate.Create(
            "BBMs:2022:3249:SD",
            new PatientId("271801"),
            sequencingId: null,
            wsiId: null,
            new Material { DerivedFrom = "BBMs:2022:0001:SD" }).Value;

        var payload = CatalogueMapper.ToPayload(sample, SamplePseudonym, PatientPseudonym);

        Assert.Null(payload.Material!.DerivedFrom);
    }

    [Fact]
    public void TheSequencingChainKeepsTheIdentifiersTheSourceAlreadyPseudonymized()
    {
        var payload = CatalogueMapper.ToPayload(Sequencing(), SamplePseudonym);

        var preparation = Assert.Single(payload.SamplePreparations);
        Assert.Equal("mmci_sampleprep_abc_RUN1", preparation.SampleprepIdentifier);
        Assert.Equal("mmci_predictive_abc_RUN1", preparation.Sequencing!.SequencingIdentifier);
        Assert.Equal("mmci_sampleprep_abc_RUN1", preparation.Sequencing.BelongsToSamplePreparation);

        var analysis = Assert.Single(preparation.Sequencing.Analyses);
        Assert.Equal("mmci_analysis_abc_RUN1", analysis.AnalysisIdentifier);
        Assert.Equal("mmci_predictive_abc_RUN1", analysis.BelongsToSequencing);
    }

    /// <summary>
    /// The one identifier in the sequencing chain the source cannot pseudonymize: it points at the
    /// biobank's material, whose id is the biobank's own.
    /// </summary>
    [Fact]
    public void TheSequencingPayloadIsKeyedOnTheSampleAndPointsAtItsMaterial()
    {
        var payload = CatalogueMapper.ToPayload(Sequencing(), SamplePseudonym);

        Assert.Equal(SamplePseudonym, payload.ExternalId);
        Assert.Equal(SamplePseudonym, payload.SampleId);
        Assert.Equal(SamplePseudonym, Assert.Single(payload.SamplePreparations).BelongsToMaterial);
    }
}
