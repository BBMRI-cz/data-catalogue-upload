using BiobankApi.Domain;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace BiobankApi.IntegrationTests;

public sealed class RepositoryTests : IDisposable
{
    private readonly SqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static Patient FullPatient() => new(
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
                eventNumber: 181,
                collectionYear: 2023,
                biopsy: "2023/2872-1",
                predictiveNumber: "2023/1052",
                samplesNo: 3,
                availableSamplesNo: 3,
                accessionNumbers: ["ACC-1"],
                diagnosis: "C56",
                pTnm: "T1N0M",
                morphology: "8380/31",
                cutTime: new DateTime(2023, 3, 24, 11, 15, 0),
                freezeTime: new DateTime(2023, 3, 24, 11, 20, 0),
                retrieved: Retrieved.Operational),
            new SerumSample(
                "BBMs:2022:3249:SD",
                "SD",
                eventNumber: 3249,
                collectionYear: 2022,
                samplesNo: 1,
                availableSamplesNo: 1,
                takingDate: new DateTime(2022, 12, 7, 0, 0, 0),
                retrieved: Retrieved.Unknown),
            new GenomeSample(
                "BBMd:2023:249:PK",
                "PK",
                eventNumber: 249,
                collectionYear: 2023,
                samplesNo: 1,
                availableSamplesNo: 1,
                takingDate: new DateTime(2023, 3, 24, 0, 0, 0),
                retrieved: Retrieved.Unknown),
        ],
        diagnosticSpecimens:
        [
            new DiagnosticSpecimen(
                "&:2022:118485",
                specimenNumber: 118485,
                year: 2022,
                materialType: "S",
                diagnosis: "C504",
                takingDate: new DateTime(2022, 9, 20, 10, 44, 0),
                retrieved: Retrieved.Unknown),
        ]);

    [Fact]
    public async Task SaveAndListRoundTripsFullPatient()
    {
        await using var context = _db.NewContext();
        var repository = new SqlBiobankRepository(context);
        var patient = FullPatient();

        await repository.SavePatientsAsync([patient], CancellationToken.None);
        var loaded = Assert.Single(await repository.ListPatientsAsync(CancellationToken.None));

        Assert.Equal("138423", loaded.PatientId);
        Assert.Equal("MOU", loaded.Biobank);
        Assert.True(loaded.Consent);
        Assert.Equal(Sex.Female, loaded.Sex);
        Assert.Equal(1943, loaded.BirthYear);
        Assert.Equal(5, loaded.BirthMonth);
        Assert.Equal(["RAD-1", "RAD-2"], loaded.AccessionNumbers);

        var tissue = Assert.Single(loaded.Samples.OfType<TissueSample>());
        Assert.Equal("C56", tissue.Diagnosis);
        Assert.Equal(new DateTime(2023, 3, 24, 11, 20, 0), tissue.FreezeTime);
        Assert.Equal(Retrieved.Operational, tissue.Retrieved);
        Assert.Equal(["ACC-1"], tissue.AccessionNumbers);

        var serum = Assert.Single(loaded.Samples.OfType<SerumSample>());
        Assert.Equal(new DateTime(2022, 12, 7, 0, 0, 0), serum.TakingDate);

        var genome = Assert.Single(loaded.Samples.OfType<GenomeSample>());
        Assert.Equal("PK", genome.MaterialType);

        var specimen = Assert.Single(loaded.DiagnosticSpecimens);
        Assert.Equal("C504", specimen.Diagnosis);
    }

    [Fact]
    public async Task RoundTripsConsentFalseStub()
    {
        await using var context = _db.NewContext();
        var repository = new SqlBiobankRepository(context);
        var stub = new Patient("P-STUB", biobank: "MOU", consent: false);

        await repository.SavePatientsAsync([stub], CancellationToken.None);
        var loaded = Assert.Single(await repository.ListPatientsAsync(CancellationToken.None));

        Assert.Equal("P-STUB", loaded.PatientId);
        Assert.False(loaded.Consent);
        Assert.Empty(loaded.Samples);
        Assert.Empty(loaded.DiagnosticSpecimens);
    }

    [Fact]
    public async Task ResavingSamePatientIsIdempotent()
    {
        await using var context = _db.NewContext();
        var repository = new SqlBiobankRepository(context);

        await repository.SavePatientsAsync([FullPatient()], CancellationToken.None);
        await repository.SavePatientsAsync([FullPatient()], CancellationToken.None);

        var loaded = Assert.Single(await repository.ListPatientsAsync(CancellationToken.None));
        Assert.Equal(3, loaded.Samples.Count);
        Assert.Single(loaded.DiagnosticSpecimens);

        Assert.Equal(1, await context.TissueSamples.CountAsync());
        Assert.Equal(1, await context.SerumSamples.CountAsync());
        Assert.Equal(1, await context.GenomeSamples.CountAsync());
        Assert.Equal(1, await context.DiagnosticSpecimens.CountAsync());
    }

    [Fact]
    public async Task ResavingReplacesChildren()
    {
        await using var context = _db.NewContext();
        var repository = new SqlBiobankRepository(context);
        await repository.SavePatientsAsync([FullPatient()], CancellationToken.None);

        var updated = new Patient(
            "138423",
            biobank: "MOU",
            consent: true,
            samples: [new TissueSample("NEW:1", "1")]);
        await repository.SavePatientsAsync([updated], CancellationToken.None);

        var loaded = Assert.Single(await repository.ListPatientsAsync(CancellationToken.None));
        Assert.Equal(["NEW:1"], loaded.Samples.Select(sample => sample.SampleId));
        Assert.Empty(loaded.DiagnosticSpecimens);

        Assert.Equal(1, await context.TissueSamples.CountAsync());
        Assert.Equal(0, await context.SerumSamples.CountAsync());
        Assert.Equal(0, await context.GenomeSamples.CountAsync());
        Assert.Equal(0, await context.DiagnosticSpecimens.CountAsync());
    }

    [Fact]
    public async Task ListPatientsEmpty()
    {
        await using var context = _db.NewContext();
        var repository = new SqlBiobankRepository(context);
        Assert.Empty(await repository.ListPatientsAsync(CancellationToken.None));
    }
}
