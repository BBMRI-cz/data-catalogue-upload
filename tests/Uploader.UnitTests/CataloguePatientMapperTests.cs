using ErrorOr;
using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain.Common;
using Xunit;

namespace Uploader.UnitTests;

/// <summary>
/// The biobank's patient record straight to the catalogue's patient rows. The pseudonyms are the fake
/// map's <c>mmci_&lt;kind&gt;_&lt;id&gt;</c>, so an assertion states both that a field was substituted
/// and which identifier it was substituted from.
/// </summary>
public sealed class CataloguePatientMapperTests
{
    private const string PatientPseudonym = "mmci_patient_P1";

    /// <summary>What <see cref="PatientPseudonym"/> derives to, and every reference to it must match.</summary>
    private const string ClinicalPseudonym = "mmci_clinical_P1";

    private const string ConsentPseudonym = "mmci_consent_P1";

    /// <summary>The one study this deployment publishes under.</summary>
    private const string StudyId = "mmci_biobank";

    private static PatientDto Patient() => new()
    {
        PatientId = "271801",
        Consent = true,
        Sex = "male",
        BirthYear = 1948,
        Samples =
        [
            new SampleDto
            {
                SampleId = "BBMs:2022:3249:SD",
                Type = "serum",
                MaterialType = "SD",
                Diagnosis = "C504",
                TakingDate = new DateTime(2022, 12, 7),
            },
        ],
    };

    private static CataloguePatientPayload Payload(PatientDto dto) =>
        CataloguePatientMapper.ToPayload(dto, PatientPseudonym, StudyId);

    [Fact]
    public void PersonalCarriesThePatientPseudonymAsItsKey()
    {
        var payload = Payload(Patient());

        Assert.Equal(PatientPseudonym, payload.ExternalId);
        Assert.Equal(PatientPseudonym, payload.Personal!.PersonalIdentifier);
        Assert.Equal(1948, payload.Personal.YearOfBirth);

        // The biobank's "male" becomes the GSSO term the catalogue's ontology actually holds.
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
        var payload = Payload(Patient());

        Assert.Equal(ConsentPseudonym, payload.Consent!.IndividualConsentIdentifier);
        Assert.Equal(PatientPseudonym, payload.Consent.PersonConsenting);
        Assert.Equal(StudyId, payload.Consent.BelongsToStudy);

        // Never known from a boolean.
        Assert.Null(payload.Consent.SigningDate);
        Assert.Null(payload.Consent.DataUsePermissions);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(null)]
    public void APatientWhoDidNotConsentGetsNoConsentRow(bool? consent)
    {
        Assert.Null(Payload(Patient() with { Consent = consent }).Consent);
    }

    [Fact]
    public void ClinicalIsDerivedFromThePseudonymAndPointsBackAtIt()
    {
        var payload = Payload(Patient());

        Assert.Equal(ClinicalPseudonym, payload.Clinical!.ClinicalIdentifier);
        Assert.Equal(PatientPseudonym, payload.Clinical.BelongsToPerson);

        // Born 1948 with no month, so the first of January; the serum was taken on 2022-12-07.
        Assert.Equal(74, payload.Clinical.AgeAtDiagnosis);

        // The catalogue's Diagnosis ontology carries ICD-10 terms, spelled with the dot.
        Assert.Equal(["C50.4"], payload.Clinical.Diagnosis);
    }

    [Fact]
    public void MapsPersonalAndClinical()
    {
        var payload = Payload(new PatientDto
        {
            PatientId = "P1",
            Consent = true,
            Sex = "male",
            BirthYear = 1980,
            Samples =
            [
                new SampleDto { SampleId = "S1", Type = "tissue", Diagnosis = "C504" },
                new SampleDto { SampleId = "S2", Type = "serum", Diagnosis = "C51" },
            ],
        });

        Assert.Equal(PatientPseudonym, payload.Personal!.PersonalIdentifier);
        Assert.Equal("assigned male at birth", payload.Personal.GenderAtBirth);
        Assert.Equal(1980, payload.Personal.YearOfBirth);
        Assert.NotNull(payload.Consent);
        Assert.Equal(["C50.4", "C51"], payload.Clinical!.Diagnosis);
        Assert.Equal(ClinicalPseudonym, payload.Clinical.ClinicalIdentifier);
    }

    [Fact]
    public void GenomeDiagnosisIsLeftOutWhileSpecimenDiagnosisIsCarried()
    {
        var payload = Payload(new PatientDto
        {
            PatientId = "P1",
            Samples =
            [
                new SampleDto { SampleId = "S1", Type = "genome", Diagnosis = "C999" },
                new SampleDto { SampleId = "S2", Type = "tissue", Diagnosis = "C504" },
                new SampleDto { SampleId = "S3", Type = "serum", Diagnosis = "C504" },
            ],
            DiagnosticSpecimens = [new SpecimenDto { SpecimenId = "D1", Diagnosis = "C777" }],
        });

        // Duplicates collapse and the order is fixed, so the fingerprint doesn't move with the payload.
        Assert.Equal(["C50.4", "C77.7"], payload.Clinical!.Diagnosis);
    }

    [Fact]
    public void AgeAtDiagnosisComesFromTheEarliestSampleEvent()
    {
        var payload = Payload(new PatientDto
        {
            PatientId = "P1",
            BirthYear = 1980,
            BirthMonth = 6,
            Samples =
            [
                new SampleDto { SampleId = "S1", Type = "serum", TakingDate = new DateTime(2021, 5, 6) },
                new SampleDto
                {
                    SampleId = "S2",
                    Type = "tissue",
                    FreezeTime = new DateTime(2020, 1, 2),
                    CutTime = new DateTime(2020, 1, 2),
                },
            ],
        });

        // Earliest event is 2020-01-02; born 1980-06-01, so the 40th birthday has not happened yet.
        Assert.Equal(39, payload.Clinical!.AgeAtDiagnosis);
    }

    /// <summary>
    /// The planner's side of the same record: the real id, which sync state is keyed on, and the
    /// consent that decides whether the patient is uploaded at all.
    /// </summary>
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(null, false)]
    public void ThePatientKeepsTheRealIdAndWhetherTheyConsented(bool? consent, bool hasConsent)
    {
        var patient = CataloguePatientMapper.ToPatient(Patient() with { Consent = consent }).Value;

        Assert.Equal(new PatientId("271801"), patient.Id);
        Assert.Equal(hasConsent, patient.HasConsent);
    }

    [Fact]
    public void BlankPatientIdIsAValidationError()
    {
        var result = CataloguePatientMapper.ToPatient(new PatientDto { PatientId = "" });

        Assert.True(result.IsError);
        Assert.Equal(ErrorType.Validation, result.FirstError.Type);
    }
}
