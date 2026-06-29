using BiobankApi.Domain;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Persistence;
using BiobankApi.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace BiobankApi.IntegrationTests;

public sealed class RepositoryTests : IDisposable
{
    private readonly SqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    private static PatientAggregate FullPatient() => PatientAggregate.Create(
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
                retrieved: Retrieved.Operational).Value,
            SerumSample.Create(
                "BBMs:2022:3249:SD",
                "SD",
                eventNumber: 3249,
                collectionYear: 2022,
                samplesNo: 1,
                availableSamplesNo: 1,
                takingDate: new DateTime(2022, 12, 7, 0, 0, 0),
                retrieved: Retrieved.Unknown).Value,
            GenomeSample.Create(
                "BBMd:2023:249:PK",
                "PK",
                eventNumber: 249,
                collectionYear: 2023,
                samplesNo: 1,
                availableSamplesNo: 1,
                takingDate: new DateTime(2023, 3, 24, 0, 0, 0),
                retrieved: Retrieved.Unknown).Value,
        ],
        diagnosticSpecimens:
        [
            DiagnosticSpecimen.Create(
                "&:2022:118485",
                specimenNumber: 118485,
                year: 2022,
                materialType: "S",
                diagnosis: "C504",
                takingDate: new DateTime(2022, 9, 20, 10, 44, 0),
                retrieved: Retrieved.Unknown).Value,
        ]).Value;

    [Fact]
    public async Task SaveAndListRoundTripsFullPatient()
    {
        await using var context = _db.NewContext();
        var repository = new SqlBiobankRepository(context);
        var patient = FullPatient();

        await repository.SavePatientsAsync([patient], CancellationToken.None);
        var loaded = Assert.Single(await repository.ListPatientsAsync(CancellationToken.None));

        Assert.Equal("138423", loaded.Id.Value);
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
        var stub = PatientAggregate.Create("P-STUB", biobank: "MOU", consent: false).Value;

        await repository.SavePatientsAsync([stub], CancellationToken.None);
        var loaded = Assert.Single(await repository.ListPatientsAsync(CancellationToken.None));

        Assert.Equal("P-STUB", loaded.Id.Value);
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

        var updated = PatientAggregate.Create(
            "138423",
            biobank: "MOU",
            consent: true,
            samples: [TissueSample.Create("NEW:1", "1").Value]).Value;
        await repository.SavePatientsAsync([updated], CancellationToken.None);

        var loaded = Assert.Single(await repository.ListPatientsAsync(CancellationToken.None));
        Assert.Equal(["NEW:1"], loaded.Samples.Select(sample => sample.Id.Value));
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

    [Fact]
    public async Task PersistsDuplicateSampleIdsWithinPatient()
    {
        // Real exports reuse a sampleId within one patient; the surrogate key must let both persist.
        await using var context = _db.NewContext();
        var repository = new SqlBiobankRepository(context);
        var patient = PatientAggregate.Create(
            "DUP-1",
            consent: true,
            samples:
            [
                SerumSample.Create("BBMs:2022:2209:SD", "SD").Value,
                SerumSample.Create("BBMs:2022:2209:SD", "SD").Value,
            ]).Value;

        await repository.SavePatientsAsync([patient], CancellationToken.None);

        Assert.Equal(2, await context.SerumSamples.CountAsync());
        var loaded = Assert.Single(await repository.ListPatientsAsync(CancellationToken.None));
        Assert.Equal(2, loaded.Samples.OfType<SerumSample>().Count());
    }

    [Fact]
    public async Task PersistsAcrossMultipleBatches()
    {
        await using var context = _db.NewContext();
        var repository = new SqlBiobankRepository(context);
        var patients = Enumerable.Range(0, 550)
            .Select(i => PatientAggregate.Create($"P{i}", consent: true).Value)
            .ToList();

        var failures = await repository.SavePatientsAsync(patients, CancellationToken.None);

        Assert.Empty(failures);
        Assert.Equal(550, (await repository.ListPatientsAsync(CancellationToken.None)).Count);
    }

    [Fact]
    public async Task IsolatesAFailingPatientAndPersistsTheRest()
    {
        await using var context = _db.NewContext(new FailOnPatientInterceptor("BAD"));
        var repository = new SqlBiobankRepository(context);

        var failures = await repository.SavePatientsAsync(
            [
                PatientAggregate.Create("G1", consent: true).Value,
                PatientAggregate.Create("BAD", consent: true).Value,
                PatientAggregate.Create("G2", consent: true).Value,
            ],
            CancellationToken.None);

        var failure = Assert.Single(failures);
        Assert.Equal("persistence", failure.Source);
        Assert.Equal("BAD", failure.Reference);

        var loaded = await repository.ListPatientsAsync(CancellationToken.None);
        Assert.Equal(["G1", "G2"], loaded.Select(patient => patient.Id.Value).OrderBy(id => id));
    }

    /// <summary>Fails the save whenever a sentinel patient is being inserted, to exercise isolation.</summary>
    private sealed class FailOnPatientInterceptor : SaveChangesInterceptor
    {
        private readonly string _failOnPatientId;

        public FailOnPatientInterceptor(string failOnPatientId) => _failOnPatientId = failOnPatientId;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            var failing = eventData.Context!.ChangeTracker.Entries<PatientEntity>()
                .Any(entry => entry.State == EntityState.Added && entry.Entity.PatientId == _failOnPatientId);
            if (failing)
            {
                throw new InvalidOperationException($"simulated persistence failure for {_failOnPatientId}");
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
