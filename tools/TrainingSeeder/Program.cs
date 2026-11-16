using System.Diagnostics;
using BiobankApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SequencingApi.Infrastructure.Persistence;
using TrainingSeeder;
using TrainingSeeder.Generation;
using TrainingSeeder.Seeding;
using TrainingSeeder.Vocabulary;

// Fills the training stack's two empty databases and exits: 0 when they hold the dataset (or already
// held data and were left alone), 1 when records were refused, 2 on bad settings.
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, args) =>
{
    args.Cancel = true;
    cancellation.Cancel();
};

var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
var settings = SeedSettings.FromConfiguration(configuration);
if (settings.IsError)
{
    Console.Error.WriteLine(settings.FirstError.Description);
    return 2;
}

var clock = Stopwatch.StartNew();
await using var biobank = SeederServices.Biobank(configuration);
await using var sequencing = SeederServices.Sequencing(configuration);
await CreateSchemaAsync<BiobankDbContext>(biobank, "biobank", cancellation.Token);
await CreateSchemaAsync<SequencingDbContext>(sequencing, "sequencing", cancellation.Token);

var vocabulary = TrainingVocabulary.Load();
var dataset = TrainingDatasetGenerator.Generate(vocabulary, settings.Value);
Console.WriteLine(
    $"Generated {dataset.Patients.Count} patients, {dataset.Samples.Count} sequencing samples, {dataset.Runs.Count} runs "
    + $"and {dataset.Panels.Count} panels (seed {settings.Value.Seed}).");

var outcome = await new DatabaseSeeder(biobank, sequencing).SeedAsync(dataset, cancellation.Token);
switch (outcome.Result)
{
    case SeedResult.LeftAlone:
        Console.WriteLine(
            $"Left alone: the databases already hold {outcome.Patients} patients and {outcome.Samples} sequencing samples. "
            + "The seeder only fills empty databases; `docker compose -f compose.training.yml down` empties the training ones.");
        return 0;
    case SeedResult.Seeded:
        Console.WriteLine(
            $"Seeded {outcome.Patients} patients, {outcome.Samples} sequencing samples, {outcome.Runs} runs "
            + $"and {outcome.Panels} panels in {clock.Elapsed.TotalSeconds:F0} s.");
        return 0;
    default:
        Console.Error.WriteLine($"{outcome.Failures.Count} records were refused:");
        foreach (var failure in outcome.Failures)
        {
            Console.Error.WriteLine($"  {failure}");
        }

        return 1;
}

// Migrates a brand-new database only. One that has migrations applied is not the seeder's to change -
// it may be a real one - and the seeder will then find it in use and leave it alone.
static async Task CreateSchemaAsync<TContext>(IServiceProvider services, string name, CancellationToken cancellationToken)
    where TContext : DbContext
{
    using var scope = services.CreateScope();
    var database = scope.ServiceProvider.GetRequiredService<TContext>().Database;
    if ((await database.GetAppliedMigrationsAsync(cancellationToken)).Any())
    {
        Console.WriteLine($"The {name} database already has a schema; leaving it as it is.");
        return;
    }

    await database.MigrateAsync(cancellationToken);
    Console.WriteLine($"Created the {name} schema.");
}
