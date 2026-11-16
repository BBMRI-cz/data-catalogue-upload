using BiobankApi.Application.Abstractions.Repositories;
using Microsoft.Extensions.DependencyInjection;
using SequencingApi.Application.Abstractions.Repositories;
using TrainingSeeder.Generation;

namespace TrainingSeeder.Seeding;

/// <summary>
/// Writes a <see cref="TrainingDataset"/> through the two services' repositories - the same save
/// path their ingests take, so the rows are exactly what an ingest would have written.
/// </summary>
/// <remarks>
/// Only ever into empty databases. The repositories delete-then-insert on the natural id, and a
/// generated id can be a real one, so seeding a database that holds real data could overwrite a real
/// patient. A database with anything in it is left alone - which is also what a second
/// <c>docker compose up</c> on a running training stack needs.
/// </remarks>
internal sealed class DatabaseSeeder
{
    private readonly IServiceProvider _biobank;
    private readonly IServiceProvider _sequencing;

    public DatabaseSeeder(IServiceProvider biobank, IServiceProvider sequencing)
    {
        _biobank = biobank;
        _sequencing = sequencing;
    }

    public async Task<SeedOutcome> SeedAsync(TrainingDataset dataset, CancellationToken cancellationToken)
    {
        if (await CountAsync(cancellationToken) is { } existing && existing.Patients + existing.Samples + existing.Runs + existing.Panels > 0)
        {
            return existing with { Result = SeedResult.LeftAlone };
        }

        var failures = new List<string>();
        using (var scope = _sequencing.CreateScope())
        {
            var services = scope.ServiceProvider;
            var panels = await services.GetRequiredService<IPanelRepository>().SavePanelsAsync(dataset.Panels, cancellationToken);
            var runs = await services.GetRequiredService<ISequencingRunRepository>().SaveRunsAsync(dataset.Runs, cancellationToken);
            var samples = await services.GetRequiredService<ISampleRepository>().SaveSamplesAsync(dataset.Samples, cancellationToken);
            failures.AddRange(panels.Concat(runs).Concat(samples).Select(error => $"{error.Source} {error.Reference}: {error.Reason}"));
        }

        using (var scope = _biobank.CreateScope())
        {
            var patients = await scope.ServiceProvider.GetRequiredService<IPatientRepository>()
                .SavePatientsAsync(dataset.Patients, cancellationToken);
            failures.AddRange(patients.Select(error => $"{error.Source} {error.Reference}: {error.Reason}"));
        }

        var written = await CountAsync(cancellationToken);
        return written with { Result = failures.Count == 0 ? SeedResult.Seeded : SeedResult.Failed, Failures = failures };
    }

    private async Task<SeedOutcome> CountAsync(CancellationToken cancellationToken)
    {
        using var biobank = _biobank.CreateScope();
        using var sequencing = _sequencing.CreateScope();
        var patients = await biobank.ServiceProvider.GetRequiredService<IPatientRepository>().ListPatientsAsync(cancellationToken);
        var summary = await sequencing.ServiceProvider.GetRequiredService<ISequencingStatsReader>().GetSummaryAsync(cancellationToken);
        return new SeedOutcome(SeedResult.Seeded, patients.Count, summary.SampleCount, summary.RunCount, summary.PanelCount, []);
    }
}
