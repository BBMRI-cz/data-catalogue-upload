using System.Net.Http.Json;
using System.Text.Json;
using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Domain;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Persistence;
using BiobankApi.Web.Endpoints;
using Xunit;

namespace BiobankApi.IntegrationTests;

/// <summary>
/// Full-stack flow against the real <see cref="SqlPatientRepository"/> (over SQLite, not a fake):
/// an ingest started by POST /admin/ingest stores a fully-populated patient, then GET /patients
/// serves it back. Exercises the endpoints, response mappers, handlers, EF Core mapping and
/// persistence at once.
/// </summary>
public sealed class IngestThenListEndToEndTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    [Fact]
    public async Task IngestThenListServesStoredPatientFromDatabase()
    {
        var patient = FullyPopulatedPatient();
        var source = new FakePatientExportSource(new ExportParseResult([patient], []));
        using var factory = new BiobankWebApplicationFactory(source);
        using var client = factory.CreateClientWithSchema();

        using var status = await AdminIngest.IngestAndWaitAsync(client);
        var run = status.RootElement.GetProperty("last_run");
        Assert.Equal(1, run.GetProperty("ingested").GetInt32());
        Assert.Equal(0, run.GetProperty("failed").GetInt32());

        var patients = await client.GetFromJsonAsync<List<PatientResponse>>("/patients", JsonOptions);

        var served = Assert.Single(patients!);
        Assert.Equal("P1", served.PatientId);
        Assert.Equal("BBM", served.Biobank);
        Assert.True(served.Consent);
        Assert.Equal("male", served.Sex);
        Assert.Equal(1980, served.BirthYear);
        Assert.Equal(4, served.BirthMonth);
        Assert.Equal(["ACC1", "ACC2"], served.AccessionNumbers);

        var tissue = Assert.Single(served.Samples, s => s.Type == "tissue");
        Assert.Equal("S-T", tissue.SampleId);
        Assert.Equal("1", tissue.MaterialType);
        Assert.Equal("Malignant tumour", tissue.MaterialTypeLabel);
        Assert.Equal("C50", tissue.Diagnosis);
        Assert.Equal("pT1", tissue.PTnm);
        Assert.Equal("operational", tissue.Retrieved);
        Assert.Equal(new DateTime(2020, 1, 2, 3, 4, 5), tissue.CutTime);

        var serum = Assert.Single(served.Samples, s => s.Type == "serum");
        Assert.Equal("SD", serum.MaterialType);
        Assert.Equal(new DateTime(2021, 5, 6, 7, 8, 9), serum.TakingDate);

        var genome = Assert.Single(served.Samples, s => s.Type == "genome");
        Assert.Equal("gD", genome.MaterialType);

        var specimen = Assert.Single(served.DiagnosticSpecimens);
        Assert.Equal("D-1", specimen.SpecimenId);
        Assert.Equal("C50", specimen.Diagnosis);
        Assert.Equal("unknown", specimen.Retrieved);
    }

    private static PatientAggregate FullyPopulatedPatient()
    {
        var tissue = TissueSample.Create(
            "S-T", "1", eventNumber: 1, collectionYear: 2020, biopsy: "B1", predictiveNumber: "PN1",
            samplesNo: 5, availableSamplesNo: 3, accessionNumbers: ["ACC1"], diagnosis: "C50", pTnm: "pT1",
            morphology: "8500/3", cutTime: new DateTime(2020, 1, 2, 3, 4, 5),
            freezeTime: new DateTime(2020, 1, 2, 4, 0, 0), retrieved: Retrieved.Operational).Value;

        var serum = SerumSample.Create(
            "S-S", "SD", diagnosis: "C50", takingDate: new DateTime(2021, 5, 6, 7, 8, 9),
            retrieved: Retrieved.Operational).Value;

        var genome = GenomeSample.Create(
            "S-G", "gD", takingDate: new DateTime(2022, 6, 7, 8, 9, 10), retrieved: Retrieved.Unknown).Value;

        var specimen = DiagnosticSpecimen.Create(
            "D-1", specimenNumber: 2, year: 2019, materialType: "S", diagnosis: "C50",
            takingDate: new DateTime(2019, 3, 4, 5, 6, 7), retrieved: Retrieved.Unknown).Value;

        return PatientAggregate.Create(
            "P1", biobank: "BBM", consent: true, sex: Sex.Male, birthYear: 1980, birthMonth: 4,
            accessionNumbers: ["ACC1", "ACC2"], samples: [tissue, serum, genome],
            diagnosticSpecimens: [specimen]).Value;
    }
}
