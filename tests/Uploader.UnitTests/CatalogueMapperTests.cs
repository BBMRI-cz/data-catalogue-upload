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

    private const string ConsentPseudonym = "mmci_consent_P1";
    private const string BiospecimenPseudonym = "mmci_biospecimen_S1";

    /// <summary>The one study this deployment publishes under.</summary>
    private const string StudyId = "mmci_biobank";

    private static PatientAggregate Patient() =>
        PatientAggregate.Create(
            "271801",
            new Personal
            {
                PersonalIdentifier = "271801",
                YearOfBirth = 1948,
                GenderAtBirth = "assigned male at birth",
            },
            new Clinical
            {
                ClinicalIdentifier = "clinical_271801",
                BelongsToPerson = "271801",
                Diagnosis = ["C50.4"],
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
                SamplingDate = "2022-12-07",
                MaterialType = "Peripheral Blood",
            },
            new Biospecimen
            {
                BiospecimenIdentifier = "biospecimen_BBMs:2022:3249:SD",
                DerivedFromMaterial = "BBMs:2022:3249:SD",
                BiospecimenForm = "Serum or Plasma",
                Quantity = 2,
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
                    BelongsToBiospecimen = "BBMs:2022:3249:SD",
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
                                DataFormatsStored = ["VCF"],
                            },
                        ],
                    },
                },
            ]).Value;

    [Fact]
    public void PersonalCarriesThePatientPseudonymAsItsKey()
    {
        var payload = CatalogueMapper.ToPayload(Patient(), PatientPseudonym, StudyId);

        Assert.Equal(PatientPseudonym, payload.ExternalId);
        Assert.Equal(PatientPseudonym, payload.Personal!.PersonalIdentifier);

        // Everything that is not an identifier is carried through untouched.
        Assert.Equal(1948, payload.Personal.YearOfBirth);
        Assert.Equal("assigned male at birth", payload.Personal.GenderAtBirth);

        // Not source data: the study every patient is published under.
        Assert.Equal([StudyId], payload.Personal.ParticipatesInStudy);
    }

    /// <summary>
    /// The biobank exports a boolean, so the consent row is entirely derived. It exists to record
    /// that consent was given and which study it covers - nothing more is known.
    /// </summary>
    [Fact]
    public void ConsentIsDerivedFromThePatientPseudonym()
    {
        var payload = CatalogueMapper.ToPayload(Patient(), PatientPseudonym, StudyId);

        Assert.Equal(ConsentPseudonym, payload.Consent!.IndividualConsentIdentifier);
        Assert.Equal(PatientPseudonym, payload.Consent.PersonConsenting);
        Assert.Equal(StudyId, payload.Consent.BelongsToStudy);

        // Never known from a boolean.
        Assert.Null(payload.Consent.SigningDate);
        Assert.Null(payload.Consent.DataUsePermissions);
    }

    [Fact]
    public void APatientWhoDidNotConsentGetsNoConsentRow()
    {
        var refused = PatientAggregate.Create("271801", null, null, hasConsent: false).Value;

        Assert.Null(CatalogueMapper.ToPayload(refused, PatientPseudonym, StudyId).Consent);
    }

    [Fact]
    public void ClinicalIsDerivedFromThePseudonymAndPointsBackAtIt()
    {
        var payload = CatalogueMapper.ToPayload(Patient(), PatientPseudonym, StudyId);

        Assert.Equal(ClinicalPseudonym, payload.Clinical!.ClinicalIdentifier);
        Assert.Equal(PatientPseudonym, payload.Clinical.BelongsToPerson);
        Assert.Equal(74, payload.Clinical.AgeAtDiagnosis);

        // The catalogue's Diagnosis ontology holds Orphanet terms and these are ICD-10, so the
        // column goes out empty rather than failing the whole row.
        Assert.Null(payload.Clinical.Diagnosis);
    }

    [Fact]
    public void MaterialCarriesBothPseudonymsAndTheDerivedDiagnosisReference()
    {
        var payload = CatalogueMapper.ToPayload(Sample(), SamplePseudonym, PatientPseudonym);

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
        var payload = CatalogueMapper.ToPayload(Sample(), SamplePseudonym, PatientPseudonym);

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
        var clinicalKey = CatalogueMapper
            .ToPayload(Patient(), PatientPseudonym, StudyId).Clinical!.ClinicalIdentifier!;
        var reference = CatalogueMapper.ToPayload(Sample(), SamplePseudonym, PatientPseudonym)
            .Material!.BelongsToDiagnosis;

        Assert.Equal([clinicalKey], reference);
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
    /// biobank's stored biospecimen, whose id is the biobank's own. It is derived rather than
    /// copied, so it always equals the biospecimen key the sample payload published.
    /// </summary>
    [Fact]
    public void TheSequencingPayloadIsKeyedOnTheSampleAndPointsAtItsBiospecimen()
    {
        var payload = CatalogueMapper.ToPayload(Sequencing(), SamplePseudonym);

        Assert.Equal(SamplePseudonym, payload.ExternalId);
        Assert.Equal(SamplePseudonym, payload.SampleId);
        Assert.Equal(
            BiospecimenPseudonym, Assert.Single(payload.SamplePreparations).BelongsToBiospecimen);
    }
}
