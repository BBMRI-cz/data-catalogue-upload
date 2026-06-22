using BiobankApi.Domain;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Persistence;
using Xunit;

namespace BiobankApi.IntegrationTests;

public sealed class MapperTests
{
    [Fact]
    public void ToEntityRoutesSamplesToTheirTables()
    {
        var patient = new Patient(
            "P1",
            consent: true,
            samples:
            [
                new TissueSample("T1", "1"),
                new SerumSample("S1", "SD"),
                new GenomeSample("G1", "PK"),
            ],
            diagnosticSpecimens: [new DiagnosticSpecimen("&:2022:1")]);

        var entity = PatientMapper.ToEntity(patient);

        Assert.Equal(new[] { "T1" }, entity.TissueSamples.Select(sample => sample.SampleId));
        Assert.Equal(new[] { "S1" }, entity.SerumSamples.Select(sample => sample.SampleId));
        Assert.Equal(new[] { "G1" }, entity.GenomeSamples.Select(sample => sample.SampleId));
        Assert.Equal(new[] { "&:2022:1" }, entity.DiagnosticSpecimens.Select(specimen => specimen.SampleId));
        Assert.Equal("P1", entity.TissueSamples[0].PatientId);
    }

    [Fact]
    public void FullPatientRoundTripsThroughMappers()
    {
        var patient = new Patient(
            "138423",
            biobank: "MOU",
            consent: true,
            sex: Sex.Female,
            birthYear: 1943,
            birthMonth: 5,
            accessionNumbers: ["RAD-1", "RAD-2"],
            samples:
            [
                new TissueSample(
                    "BBM:2023:181:1",
                    "1",
                    accessionNumbers: ["ACC-1"],
                    diagnosis: "C56",
                    cutTime: new DateTime(2023, 3, 24, 11, 15, 0),
                    freezeTime: new DateTime(2023, 3, 24, 11, 20, 0),
                    retrieved: Retrieved.Operational),
            ],
            diagnosticSpecimens:
            [
                new DiagnosticSpecimen(
                    "&:2022:118485",
                    diagnosis: "C504",
                    takingDate: new DateTime(2022, 9, 20, 10, 44, 0),
                    retrieved: Retrieved.Unknown),
            ]);

        var restored = PatientMapper.ToDomain(PatientMapper.ToEntity(patient));

        Assert.Equal(patient.PatientId, restored.PatientId);
        Assert.Equal(patient.Biobank, restored.Biobank);
        Assert.Equal(patient.Consent, restored.Consent);
        Assert.Equal(patient.Sex, restored.Sex);
        Assert.Equal(patient.BirthYear, restored.BirthYear);
        Assert.Equal(patient.BirthMonth, restored.BirthMonth);
        Assert.Equal(patient.AccessionNumbers, restored.AccessionNumbers);

        var tissue = Assert.IsType<TissueSample>(Assert.Single(restored.Samples));
        Assert.Equal("BBM:2023:181:1", tissue.SampleId);
        Assert.Equal(["ACC-1"], tissue.AccessionNumbers);
        Assert.Equal("C56", tissue.Diagnosis);
        Assert.Equal(new DateTime(2023, 3, 24, 11, 15, 0), tissue.CutTime);
        Assert.Equal(new DateTime(2023, 3, 24, 11, 20, 0), tissue.FreezeTime);
        Assert.Equal(Retrieved.Operational, tissue.Retrieved);

        var specimen = Assert.Single(restored.DiagnosticSpecimens);
        Assert.Equal("&:2022:118485", specimen.SampleId);
        Assert.Equal("C504", specimen.Diagnosis);
        Assert.Equal(new DateTime(2022, 9, 20, 10, 44, 0), specimen.TakingDate);
        Assert.Equal(Retrieved.Unknown, specimen.Retrieved);
    }
}
