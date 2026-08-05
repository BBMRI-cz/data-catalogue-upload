using Uploader.Application.Dtos;
using Uploader.Application.Mapping;
using Uploader.Domain;
using Uploader.Domain.Common;
using Uploader.Infrastructure.Http;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// Wire to domain in one pass: a recorded <c>GET /patients</c> body goes through the real
/// <see cref="HttpSourceDataGateway"/> and the mappers, and the assembled aggregates are inspected.
/// This is what catches a serializer mismatch (a key like <c>p_tnm</c> that no DTO property lands on),
/// which a mapper unit test cannot see because it starts from an already-built DTO.
/// </summary>
public sealed class BiobankFetchTests
{
    private static async Task<IReadOnlyList<PatientDto>> FetchAsync()
    {
        var gateway = new HttpSourceDataGateway(BiobankResponse.ClientFactory(BiobankResponse.Json()));
        return await gateway.FetchPatientsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task PatientArrivesWithDemographicsAndDiagnoses()
    {
        var dtos = await FetchAsync();

        var dto = Assert.Single(dtos, p => p.PatientId == "P1");
        var patient = PatientMapper.ToPatient(dto).Value;

        Assert.Equal(new PatientId("P1"), patient.Id);
        Assert.True(patient.HasConsent);
        Assert.Equal("P1", patient.Personal!.PersonalIdentifier);
        Assert.Equal("male", patient.Personal.GenderAtBirth);
        Assert.Equal(1980, patient.Personal.YearOfBirth);

        Assert.Equal("clinical_P1", patient.Clinical!.ClinicalIdentifier);
        Assert.Equal("P1", patient.Clinical.BelongsToPerson);

        // Tissue and serum both say C504, the specimen says C777, the genome sample says nothing.
        Assert.Equal(["C50.4", "C77.7"], patient.Clinical.ClinicalDiagnosis);

        // Earliest event is the tissue freeze on 2020-01-02; born 1980-04.
        Assert.Equal(39, patient.Clinical.AgeAtDiagnosis);
    }

    [Fact]
    public async Task SamplesArriveWithMaterialAndTheSequencingLink()
    {
        var dto = Assert.Single(await FetchAsync(), p => p.PatientId == "P1");
        var patientId = new PatientId("P1");

        var samples = (dto.Samples ?? [])
            .Select(sample => SampleMapper.ToSample(sample, patientId, dto.Biobank).Value)
            .ToDictionary(sample => sample.Id.Value);

        Assert.Equal(3, samples.Count);

        var tissue = samples["S-T"];
        Assert.Equal("1", tissue.Material!.BiospecimenType);
        Assert.Equal("BBM", tissue.Material.PhysicalLocation);
        Assert.Equal("S-T", tissue.Material.MaterialIdentifier);
        Assert.Equal("P1", tissue.Material.CollectedFromPerson);
        Assert.Equal(["clinical_P1"], tissue.Material.BelongsToDiagnosis);
        Assert.Equal("2020-01-02T03:04:05", tissue.Material.SamplingTimestamp);
        Assert.Equal("2020-01-02T04:00:00", tissue.Material.RegistrationTimestamp);
        Assert.Equal(new SequencingId("2020/1052"), tissue.SequencingId);

        // The biobank serves a `biopsy` but no bioptic number: the WSI link waits for #31.
        Assert.Null(tissue.WsiId);

        var serum = samples["S-S"];
        Assert.Equal("SD", serum.Material!.BiospecimenType);
        Assert.Equal("2021-05-06T07:08:09", serum.Material.SamplingTimestamp);
        Assert.Equal("2021-05-06T07:08:09", serum.Material.RegistrationTimestamp);
        Assert.Null(serum.SequencingId);

        Assert.Equal("gD", samples["S-G"].Material!.BiospecimenType);
    }

    [Fact]
    public async Task PatientWithoutConsentIsNotUploadEligible()
    {
        var dto = Assert.Single(await FetchAsync(), p => p.PatientId == "P2");

        var patient = PatientMapper.ToPatient(dto).Value;

        Assert.False(patient.HasConsent);
        Assert.False(new PatientCatalogueData { Patient = patient }.IsUploadEligible);
    }
}
