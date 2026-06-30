using BiobankApi.Domain;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Mapping;
using Xunit;

namespace BiobankApi.IntegrationTests;

public sealed class MapperTests
{
    [Fact]
    public void ToEntityRoutesSamplesToTheirTables()
    {
        var patient = PatientAggregate.Create(
            "P1",
            consent: true,
            samples:
            [
                TissueSample.Create("T1", "1").Value,
                SerumSample.Create("S1", "SD").Value,
                GenomeSample.Create("G1", "PK").Value,
            ],
            diagnosticSpecimens: [DiagnosticSpecimen.Create("&:2022:1").Value]).Value;

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
        var patient = PatientAggregate.Create(
            "138423",
            biobank: "MOU",
            consent: true,
            sex: Sex.Female,
            birthYear: 1943,
            birthMonth: 5,
            accessionNumbers: ["RAD-1", "RAD-2"],
            samples:
            [
                TissueSample.Create(
                    "BBM:2023:181:1",
                    "1",
                    accessionNumbers: ["ACC-1"],
                    diagnosis: "C56",
                    cutTime: new DateTime(2023, 3, 24, 11, 15, 0),
                    freezeTime: new DateTime(2023, 3, 24, 11, 20, 0),
                    retrieved: Retrieved.Operational).Value,
            ],
            diagnosticSpecimens:
            [
                DiagnosticSpecimen.Create(
                    "&:2022:118485",
                    diagnosis: "C504",
                    takingDate: new DateTime(2022, 9, 20, 10, 44, 0),
                    retrieved: Retrieved.Unknown).Value,
            ]).Value;

        var restored = PatientMapper.ToDomain(PatientMapper.ToEntity(patient));

        Assert.Equal(patient.Id.Value, restored.Id.Value);
        Assert.Equal(patient.Biobank, restored.Biobank);
        Assert.Equal(patient.Consent, restored.Consent);
        Assert.Equal(patient.Sex, restored.Sex);
        Assert.Equal(patient.BirthYear, restored.BirthYear);
        Assert.Equal(patient.BirthMonth, restored.BirthMonth);
        Assert.Equal(patient.AccessionNumbers, restored.AccessionNumbers);

        var tissue = Assert.IsType<TissueSample>(Assert.Single(restored.Samples));
        Assert.Equal("BBM:2023:181:1", tissue.Id.Value);
        Assert.Equal(["ACC-1"], tissue.AccessionNumbers);
        Assert.Equal("C56", tissue.Diagnosis);
        Assert.Equal(new DateTime(2023, 3, 24, 11, 15, 0), tissue.CutTime);
        Assert.Equal(new DateTime(2023, 3, 24, 11, 20, 0), tissue.FreezeTime);
        Assert.Equal(Retrieved.Operational, tissue.Retrieved);

        var specimen = Assert.Single(restored.DiagnosticSpecimens);
        Assert.Equal("&:2022:118485", specimen.Id.Value);
        Assert.Equal("C504", specimen.Diagnosis);
        Assert.Equal(new DateTime(2022, 9, 20, 10, 44, 0), specimen.TakingDate);
        Assert.Equal(Retrieved.Unknown, specimen.Retrieved);
    }

    // Full-field parity guard: with the mappers hand-written, a dropped/mistyped field is no longer a
    // compile error, so every field of every sample type and the specimen is asserted after a round trip.
    [Fact]
    public void EverySampleAndSpecimenFieldRoundTrips()
    {
        var tissue = TissueSample.Create(
            "T1", "1", eventNumber: 2, collectionYear: 2019, biopsy: "BX", predictiveNumber: "PR",
            samplesNo: 5, availableSamplesNo: 4, accessionNumbers: ["a1", "a2"], diagnosis: "C50",
            pTnm: "pT1N0M0", morphology: "8000/3", cutTime: new DateTime(2019, 1, 2, 3, 4, 5),
            freezeTime: new DateTime(2019, 1, 2, 3, 30, 0), retrieved: Retrieved.Operational).Value;
        var serum = SerumSample.Create(
            "S1", "SD", eventNumber: 1, collectionYear: 2020, biopsy: "B2", predictiveNumber: "P2",
            samplesNo: 3, availableSamplesNo: 2, accessionNumbers: ["b1"], diagnosis: "C51",
            takingDate: new DateTime(2020, 6, 7, 8, 9, 10), retrieved: Retrieved.Unknown).Value;
        var genome = GenomeSample.Create(
            "G1", "PK", eventNumber: 0, collectionYear: 2021, biopsy: "B3", predictiveNumber: "P3",
            samplesNo: 7, availableSamplesNo: 7, accessionNumbers: ["c1"],
            takingDate: new DateTime(2021, 11, 12, 13, 14, 15), retrieved: Retrieved.Operational).Value;
        var specimen = DiagnosticSpecimen.Create(
            "&:2022:1", specimenNumber: 9, year: 2018, materialType: "S", diagnosis: "C52",
            takingDate: new DateTime(2018, 2, 3, 4, 5, 6), retrieved: Retrieved.Unknown).Value;

        var patient = PatientAggregate.Create(
            "P1", biobank: "MOU", consent: true, sex: Sex.Male, birthYear: 1955, birthMonth: 8,
            accessionNumbers: ["RAD-1"], samples: [tissue, serum, genome],
            diagnosticSpecimens: [specimen]).Value;

        var restored = PatientMapper.ToDomain(PatientMapper.ToEntity(patient));

        var t = Assert.IsType<TissueSample>(Assert.Single(restored.Samples, s => s is TissueSample));
        AssertCommonSampleFields(tissue, t);
        Assert.Equal(tissue.Diagnosis, t.Diagnosis);
        Assert.Equal(tissue.PTnm, t.PTnm);
        Assert.Equal(tissue.Morphology, t.Morphology);
        Assert.Equal(tissue.CutTime, t.CutTime);
        Assert.Equal(tissue.FreezeTime, t.FreezeTime);
        Assert.Equal(tissue.Retrieved, t.Retrieved);

        var s = Assert.IsType<SerumSample>(Assert.Single(restored.Samples, x => x is SerumSample));
        AssertCommonSampleFields(serum, s);
        Assert.Equal(serum.Diagnosis, s.Diagnosis);
        Assert.Equal(serum.TakingDate, s.TakingDate);
        Assert.Equal(serum.Retrieved, s.Retrieved);

        var g = Assert.IsType<GenomeSample>(Assert.Single(restored.Samples, x => x is GenomeSample));
        AssertCommonSampleFields(genome, g);
        Assert.Equal(genome.TakingDate, g.TakingDate);
        Assert.Equal(genome.Retrieved, g.Retrieved);

        var d = Assert.Single(restored.DiagnosticSpecimens);
        Assert.Equal(specimen.Id.Value, d.Id.Value);
        Assert.Equal(specimen.SpecimenNumber, d.SpecimenNumber);
        Assert.Equal(specimen.Year, d.Year);
        Assert.Equal(specimen.MaterialType, d.MaterialType);
        Assert.Equal(specimen.Diagnosis, d.Diagnosis);
        Assert.Equal(specimen.TakingDate, d.TakingDate);
        Assert.Equal(specimen.Retrieved, d.Retrieved);
    }

    private static void AssertCommonSampleFields(Sample expected, Sample actual)
    {
        Assert.Equal(expected.Id.Value, actual.Id.Value);
        Assert.Equal(expected.MaterialType, actual.MaterialType);
        Assert.Equal(expected.EventNumber, actual.EventNumber);
        Assert.Equal(expected.CollectionYear, actual.CollectionYear);
        Assert.Equal(expected.Biopsy, actual.Biopsy);
        Assert.Equal(expected.PredictiveNumber, actual.PredictiveNumber);
        Assert.Equal(expected.SamplesNo, actual.SamplesNo);
        Assert.Equal(expected.AvailableSamplesNo, actual.AvailableSamplesNo);
        Assert.Equal(expected.AccessionNumbers, actual.AccessionNumbers);
    }
}
