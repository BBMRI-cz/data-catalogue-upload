using System.Text.RegularExpressions;
using BiobankApi.Application.Abstractions.Repositories;
using BiobankApi.Domain.Patients;
using BiobankApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Infrastructure.Persistence;
using TrainingSeeder.Generation;
using TrainingSeeder.Seeding;
using TrainingSeeder.Vocabulary;
using Xunit;

namespace TrainingSeeder.IntegrationTests;

/// <summary>
/// The seeder end to end against SQLite, through the services' real repositories. It builds every
/// record through the domain factories, so a domain change that breaks it - a new invariant, a
/// renamed value - fails here instead of in someone's <c>docker compose up</c>.
/// </summary>
public sealed class TrainingSeederTests
{
    // Small enough to seed in about a second, large enough to hold every edge case.
    private const int Patients = 600;

    private static readonly TrainingVocabulary Vocabulary = TrainingVocabulary.Load();

    [Fact]
    public async Task SeedsBothDatabasesThroughTheirRepositories()
    {
        await using var databases = await SqliteTrainingDatabases.CreateAsync();
        var dataset = Generate();

        var outcome = await databases.Seeder.SeedAsync(dataset, CancellationToken.None);

        Assert.Equal(SeedResult.Seeded, outcome.Result);
        Assert.Empty(outcome.Failures);
        Assert.Equal(Patients, outcome.Patients);
        Assert.Equal(dataset.Samples.Count, outcome.Samples);
        Assert.Equal(dataset.Runs.Count, outcome.Runs);
        Assert.Equal(Vocabulary.Sequencing.Panels.Count, outcome.Panels);
    }

    [Fact]
    public async Task EveryPredictiveNumberAPatientCarriesFindsSequencing()
    {
        await using var databases = await SqliteTrainingDatabases.CreateAsync();
        await databases.Seeder.SeedAsync(Generate(), CancellationToken.None);
        using var biobank = databases.Biobank.CreateScope();
        using var sequencing = databases.Sequencing.CreateScope();
        var samples = sequencing.ServiceProvider.GetRequiredService<ISampleRepository>();

        var patients = await biobank.ServiceProvider.GetRequiredService<IPatientRepository>().ListPatientsAsync(CancellationToken.None);
        var numbers = patients
            .SelectMany(patient => patient.Samples)
            .Select(sample => sample.PredictiveNumber)
            .OfType<string>()
            .Distinct()
            .ToList();

        // The real sequencing database has more cases than the biobank has predictive numbers, so
        // every number a patient carries is handed to a sequencing case. This is the query behind
        // GET /sequencing?predictive_number=.
        Assert.NotEmpty(numbers);
        foreach (var number in numbers)
        {
            Assert.NotEmpty(await samples.GetSamplesByPredictiveNumberAsync(number, CancellationToken.None));
        }
    }

    [Fact]
    public async Task ASampleSheetOrMiSeqFormIsFoundByTheBiobankForm()
    {
        await using var databases = await SqliteTrainingDatabases.CreateAsync();
        var dataset = Generate();
        await databases.Seeder.SeedAsync(dataset, CancellationToken.None);
        using var sequencing = databases.Sequencing.CreateScope();
        var samples = sequencing.ServiceProvider.GetRequiredService<ISampleRepository>();

        var otherForms = dataset.Samples.Where(sample => !BiobankForm().IsMatch(sample.PredictiveNumber!)).ToList();

        Assert.NotEmpty(otherForms);
        foreach (var sample in otherForms)
        {
            var biobankForm = sample.PredictiveKey!.Replace('-', '/');
            var found = await samples.GetSamplesByPredictiveNumberAsync(biobankForm, CancellationToken.None);
            Assert.Contains(found, match => match.Id == sample.Id);
        }
    }

    [Fact]
    public void TheDatasetHoldsEveryEdgeCase()
    {
        var dataset = Generate();
        var predictiveNumbers = dataset.Samples.Select(sample => sample.PredictiveNumber).OfType<string>().ToList();
        var tissue = dataset.Patients.SelectMany(patient => patient.Samples).OfType<TissueSample>();

        Assert.Contains(dataset.Patients, patient => patient.Consent == false);
        Assert.Contains(dataset.Patients, patient => patient.Consent == true && patient.Samples.Count == 0);
        Assert.Contains(
            tissue.Where(sample => sample.PredictiveNumber is not null).GroupBy(sample => sample.PredictiveNumber),
            surgery => surgery.Count() > 1);
        Assert.Contains(dataset.Runs, run => run.SourceClass == "mamma-print");
        Assert.Contains(dataset.Samples, sample => sample.RunSamples.Count > 1);
        Assert.Contains(predictiveNumbers, number => BiobankForm().IsMatch(number));
        Assert.Contains(predictiveNumbers, number => SampleSheetForm().IsMatch(number));
        Assert.Contains(predictiveNumbers, number => MiSeqForm().IsMatch(number));
    }

    [Fact]
    public void EveryLibraryPointsAtAPanelTheDatasetHas()
    {
        var dataset = Generate();
        var panels = dataset.Panels.Select(panel => panel.Id).ToHashSet();

        var libraries = dataset.Samples
            .SelectMany(sample => sample.RunSamples)
            .Select(runSample => runSample.LibraryPreparation?.PanelId)
            .OfType<SequencingApi.Domain.Common.PanelId>()
            .ToList();

        Assert.NotEmpty(libraries);
        Assert.All(libraries, panel => Assert.Contains(panel, panels));
    }

    [Fact]
    public async Task TheSameSettingsWriteTheSameRows()
    {
        await using var first = await SqliteTrainingDatabases.CreateAsync();
        await using var second = await SqliteTrainingDatabases.CreateAsync();
        await using var otherSeed = await SqliteTrainingDatabases.CreateAsync();

        await first.Seeder.SeedAsync(Generate(), CancellationToken.None);
        await second.Seeder.SeedAsync(Generate(), CancellationToken.None);
        await otherSeed.Seeder.SeedAsync(Generate(seed: 7), CancellationToken.None);

        Assert.Equal(first.Dump(), second.Dump());
        Assert.NotEqual(first.Dump(), otherSeed.Dump());
    }

    [Fact]
    public async Task LeavesDatabasesThatHoldDataAlone()
    {
        await using var databases = await SqliteTrainingDatabases.CreateAsync();
        await databases.Seeder.SeedAsync(Generate(), CancellationToken.None);
        var before = databases.Dump();

        var outcome = await databases.Seeder.SeedAsync(Generate(seed: 7), CancellationToken.None);

        Assert.Equal(SeedResult.LeftAlone, outcome.Result);
        Assert.Equal(Patients, outcome.Patients);
        Assert.Equal(before, databases.Dump());
    }

    [Fact]
    public async Task LeavesBothAloneWhenEitherHoldsData()
    {
        await using var databases = await SqliteTrainingDatabases.CreateAsync();
        var dataset = Generate();
        using (var scope = databases.Sequencing.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IPanelRepository>().SavePanelsAsync(dataset.Panels, CancellationToken.None);
        }

        var outcome = await databases.Seeder.SeedAsync(dataset, CancellationToken.None);

        Assert.Equal(SeedResult.LeftAlone, outcome.Result);
        Assert.Equal(0, outcome.Patients);
        Assert.Equal(0, outcome.Samples);
    }

    [Fact]
    public void ByDefaultTheDatasetHasTheRealSizeAndConsentRate()
    {
        var dataset = TrainingDatasetGenerator.Generate(Vocabulary, new SeedSettings(null, SeedSettings.DefaultSeed));

        var realWithdrawn = (double)Vocabulary.Biobank.Values.CountOf("patient.consent", false) / Vocabulary.Biobank.Patients;
        var withdrawn = (double)dataset.Patients.Count(patient => patient.Consent == false) / dataset.Patients.Count;

        Assert.Equal(Vocabulary.Biobank.Patients, dataset.Patients.Count);
        Assert.Equal(Vocabulary.Sequencing.Runs, dataset.Runs.Count);
        Assert.InRange(withdrawn, realWithdrawn - 0.02, realWithdrawn + 0.02);
    }

    [Fact]
    public void AtFullSizeEveryPredictiveNumberAPatientCarriesHasASequencingCase()
    {
        var dataset = TrainingDatasetGenerator.Generate(Vocabulary, new SeedSettings(null, SeedSettings.DefaultSeed));

        var keys = dataset.Samples.Select(sample => sample.PredictiveKey).ToHashSet();
        var numbers = dataset.Patients
            .SelectMany(patient => patient.Samples)
            .Select(sample => sample.PredictiveNumber)
            .OfType<string>()
            .Distinct()
            .ToList();

        Assert.All(numbers, number => Assert.Contains(number.Replace('/', '-'), keys));
    }

    [Fact]
    public void ASampleIsSequencedNoEarlierThanTheYearOfItsPredictiveNumber()
    {
        var dataset = TrainingDatasetGenerator.Generate(Vocabulary, new SeedSettings(null, SeedSettings.DefaultSeed));
        var runDates = dataset.Runs.ToDictionary(run => run.Id, run => run.RunDate);

        var early = dataset.Samples.Count(sample =>
            int.Parse(sample.PredictiveKey!.Split('-')[0], System.Globalization.CultureInfo.InvariantCulture)
                > sample.RunSamples.Min(runSample => runDates[runSample.RunId])!.Value.Year);

        // Only the biobank numbers no case of a fitting year was left for may come out early.
        Assert.InRange((double)early / dataset.Samples.Count, 0, 0.01);
    }

    [Fact]
    public void TheMigrationsTheStackAppliesMatchTheModelTheSeederWrites()
    {
        // The training stack migrates Postgres; these tests build SQLite from the model. A model change
        // without its migration would pass here and break the stack, so the two are compared.
        using var biobank = new BiobankDbContext(
            new DbContextOptionsBuilder<BiobankDbContext>().UseNpgsql("Host=unused").Options);
        using var sequencing = new SequencingDbContext(
            new DbContextOptionsBuilder<SequencingDbContext>().UseNpgsql("Host=unused").Options);

        Assert.False(biobank.Database.HasPendingModelChanges(), "The biobank model has changes no migration applies.");
        Assert.False(sequencing.Database.HasPendingModelChanges(), "The sequencing model has changes no migration applies.");
    }

    private static TrainingDataset Generate(int seed = SeedSettings.DefaultSeed) =>
        TrainingDatasetGenerator.Generate(Vocabulary, new SeedSettings(Patients, seed));

    private static Regex BiobankForm() => new(@"^20\d\d/\d+$");

    private static Regex SampleSheetForm() => new(@"^20\d\d_\d+_(DNA|RNA)$");

    private static Regex MiSeqForm() => new(@"^\d+-\d\d$");
}
