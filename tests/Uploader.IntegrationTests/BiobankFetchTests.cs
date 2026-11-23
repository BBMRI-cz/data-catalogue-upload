using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain;
using Uploader.Domain.Common;
using Uploader.Infrastructure.Configuration;
using Uploader.Infrastructure.Http;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// Wire to catalogue rows in one pass: a recorded <c>GET /patients</c> body goes through the real
/// <see cref="HttpSourceDataGateway"/> and the payload mappers, and the rows and the planner's
/// aggregates are inspected. This is what catches a serializer mismatch (a key like <c>p_tnm</c> that
/// no DTO property lands on), which a mapper unit test cannot see because it starts from an
/// already-built DTO. The pseudonyms are the fake map's <c>mmci_&lt;kind&gt;_&lt;id&gt;</c>.
/// </summary>
public sealed class BiobankFetchTests
{
    private const string PatientPseudonym = "mmci_patient_P1";

    private static async Task<IReadOnlyList<PatientDto>> FetchAsync()
    {
        var gateway = new HttpSourceDataGateway(
            RecordedResponse.ClientFactory(RecordedResponse.Patients()), new UploaderOptions());
        return (await gateway.FetchPatientsAsync(CancellationToken.None)).Value;
    }

    [Fact]
    public async Task PatientArrivesWithDemographicsAndDiagnoses()
    {
        var dtos = await FetchAsync();

        var dto = Assert.Single(dtos, p => p.PatientId == "P1");
        var patient = CataloguePatientMapper.ToPatient(dto).Value;
        var payload = CataloguePatientMapper.ToPayload(dto, PatientPseudonym, "study_1");

        Assert.Equal(new PatientId("P1"), patient.Id);
        Assert.True(patient.HasConsent);
        Assert.Equal(PatientPseudonym, payload.Personal!.PersonalIdentifier);
        Assert.Equal("assigned male at birth", payload.Personal.GenderAtBirth);
        Assert.Equal(1980, payload.Personal.YearOfBirth);

        Assert.Equal("mmci_clinical_P1", payload.Clinical!.ClinicalIdentifier);
        Assert.Equal(PatientPseudonym, payload.Clinical.BelongsToPerson);

        // Tissue and serum both say C504, the specimen says C777, the genome sample says nothing.
        Assert.Equal(["C50.4", "C77.7"], payload.Clinical.Diagnosis);

        // Earliest event is the tissue freeze on 2020-01-02; born 1980-04.
        Assert.Equal(39, payload.Clinical.AgeAtDiagnosis);
    }

    [Fact]
    public async Task SamplesArriveWithMaterialAndTheSequencingLink()
    {
        var dto = Assert.Single(await FetchAsync(), p => p.PatientId == "P1");
        var patientId = new PatientId("P1");

        var samples = (dto.Samples ?? [])
            .ToDictionary(
                sample => sample.SampleId!,
                sample => (
                    Sample: CatalogueSampleMapper.ToSample(sample, patientId).Value,
                    Payload: CatalogueSampleMapper.ToPayload(
                        sample, $"mmci_sample_{sample.SampleId}", PatientPseudonym, dto.Biobank)));

        Assert.Equal(3, samples.Count);

        var tissue = samples["S-T"].Payload;
        Assert.Equal("mmci_sample_S-T", tissue.Material!.MaterialIdentifier);
        Assert.Equal(PatientPseudonym, tissue.Material.CollectedFromPerson);
        Assert.Equal(["mmci_clinical_P1"], tissue.Material.BelongsToDiagnosis);
        Assert.Equal("2020-01-02", tissue.Material.SamplingDate);

        // Material code "1", a malignant tumour, splits across the two v2 tables: what was
        // collected and what state it was in on Material, how it is stored on Biospecimens.
        Assert.Equal("Solid Tissue Specimen", tissue.Material.MaterialType);
        Assert.Equal("Tumor", tissue.Material.PathologicalState);
        Assert.Equal("Frozen Tissue", tissue.Biospecimen!.BiospecimenForm);
        Assert.Equal(
            "Bank of Biological Material, Masaryk Memorial Cancer Institute",
            tissue.Biospecimen.ManagingBiobank);
        Assert.Equal(3, tissue.Biospecimen.Quantity);
        Assert.Equal(new SequencingId("2020/1052"), samples["S-T"].Sample.SequencingId);

        // The biobank serves a `biopsy` but no bioptic number: the WSI link waits for #31.
        Assert.Null(samples["S-T"].Sample.WsiId);

        // "SD" is nitrogen-stored serum: a blood draw, kept as a serum fraction. The ontology
        // does not separate serum from plasma, and nothing says whether the tumour was involved.
        var serum = samples["S-S"].Payload;
        Assert.Equal("Peripheral Blood", serum.Material!.MaterialType);
        Assert.Null(serum.Material.PathologicalState);
        Assert.Equal("Serum or Plasma", serum.Biospecimen!.BiospecimenForm);
        Assert.Equal("2021-05-06", serum.Material.SamplingDate);
        Assert.Null(samples["S-S"].Sample.SequencingId);

        // "gD" is DNA extracted from that same blood.
        var genome = samples["S-G"].Payload;
        Assert.Equal("Peripheral Blood", genome.Material!.MaterialType);
        Assert.Equal("Blood DNA", genome.Biospecimen!.BiospecimenForm);
    }

    [Fact]
    public async Task PatientWithoutConsentIsNotUploadEligible()
    {
        var dto = Assert.Single(await FetchAsync(), p => p.PatientId == "P2");

        var patient = CataloguePatientMapper.ToPatient(dto).Value;

        Assert.False(patient.HasConsent);
        Assert.False(new PatientCatalogueData { Patient = patient }.IsUploadEligible);
    }
}
