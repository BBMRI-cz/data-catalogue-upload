using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Domain;
using SequencingApi.Domain.Common;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// The whole pipeline over the real <c>TestData</c> tree and the real repositories (on SQLite, not
/// fakes): an ingest started by <c>POST /admin/ingest</c> reads the source, maps it, persists it, and
/// the stored aggregate is then read back out.
/// </summary>
/// <remarks>
/// The Quartz <c>IngestionJob</c> dispatches the identical <see cref="IngestRecordsCommand"/> through
/// the same runner; <see cref="ScheduledIngestTests"/> covers that path.
/// </remarks>
public sealed class IngestEndToEndTests
{
    [Fact]
    public async Task IngestStoresTheWholeTreeAndServesASampleBackFromTheDatabase()
    {
        using var database = new TempSqliteFile();
        using var rootFactory = new WebApplicationFactory<Program>();
        var factory = SqliteWebHost.Configure(rootFactory, database.Path);
        using var client = factory.CreateClient();

        var summary = await AdminIngest.IngestAndWaitAsync(client);

        // Four distinct samples across three runs (p0001, p0002, p0009, p0050); the orphan folder
        // p0003 is reported instead of ingested, which is why this is not five.
        Assert.Equal(4, summary.IngestedSamples);
        Assert.Equal(3, summary.IngestedRuns);
        Assert.Equal(2, summary.IngestedPanels);

        // Problems are reported rather than thrown - the orphan folder and the duplicate run copy.
        // ErrorCount is the length of that list and nothing else, which is what this pins.
        Assert.NotEmpty(summary.Errors);
        Assert.Equal(summary.ErrorCount, summary.Errors.Count);

        using var readScope = factory.Services.CreateScope();
        var samples = readScope.ServiceProvider.GetRequiredService<ISampleRepository>();

        var sample = await samples.GetSampleAsync(new SampleId("p0001"), default);

        Assert.NotNull(sample);
        Assert.Equal("mmci_predictive", sample!.IdScheme);
        Assert.Equal("4-21", sample.PredictiveNumber);
        Assert.Equal(3, sample.RunSamples.Count);

        var analysed = Assert.Single(
            sample.RunSamples,
            runSample => runSample.RunId.Value == "240104_M02340_0399_LCBRW");
        Assert.Equal(2, analysed.Files.Count(file => file.Role == FileRole.Fastq));
        Assert.Equal("hypercap-mop-20240101", analysed.LibraryPreparation!.PanelId!.Value.Value);

        var analysis = Assert.Single(analysed.Analyses);
        Assert.Equal("NextGENe", analysis.PipelineName);
        // 640,32 as the coverage report states it — the fractional depth survives the round trip
        // through PostgreSQL and the wire, rather than being rounded on the way in.
        Assert.Equal(640.32, analysis.Quality!.MedianReadDepth!.Value, precision: 2);
    }

    [Fact]
    public async Task IngestingTwiceIsIdempotentAndDoesNotDuplicateChildren()
    {
        using var database = new TempSqliteFile();
        using var rootFactory = new WebApplicationFactory<Program>();
        var factory = SqliteWebHost.Configure(rootFactory, database.Path);
        using var client = factory.CreateClient();

        var first = await AdminIngest.IngestAndWaitAsync(client);
        var second = await AdminIngest.IngestAndWaitAsync(client);

        // The scheduled job runs weekly over a tree that mostly has not changed, so a second run must
        // land on exactly the same numbers.
        Assert.Equal(first.IngestedSamples, second.IngestedSamples);
        Assert.Equal(first.IngestedRuns, second.IngestedRuns);
        Assert.Equal(first.IngestedPanels, second.IngestedPanels);
        Assert.Equal(first.ErrorCount, second.ErrorCount);

        using var readScope = factory.Services.CreateScope();

        // The repositories delete-then-insert, and a broken cascade only shows as duplicated children
        // on the second save - which a summary count would never reveal.
        var stats = await readScope.ServiceProvider
            .GetRequiredService<ISequencingStatsReader>()
            .GetSummaryAsync(default);

        Assert.Equal(4, stats.SampleCount);
        Assert.Equal(3, stats.RunCount);
        Assert.Equal(2, stats.PanelCount);
        Assert.Equal(1, stats.ResequencedSampleCount);   // p0001, in three runs

        var sample = await readScope.ServiceProvider
            .GetRequiredService<ISampleRepository>()
            .GetSampleAsync(new SampleId("p0001"), default);

        Assert.Equal(3, sample!.RunSamples.Count);
        Assert.Equal(2, Assert.Single(
            sample.RunSamples,
            runSample => runSample.RunId.Value == "240104_M02340_0399_LCBRW").Files.Count);
    }

    /// <summary>
    /// A run that leaves the source must leave the database with it. Saving alone only ever adds and
    /// replaces, so before the ingest cleared first, a withdrawn run kept its samples, its files and
    /// its predictive numbers indefinitely — served by <c>GET /sequencing</c> as though still real,
    /// with the ingest reporting only what it had read and nothing about what it had not.
    /// </summary>
    [Fact]
    public async Task ARunWithdrawnFromTheSourceIsRemovedOnTheNextIngest()
    {
        // A copy, because this test mutates the tree and the fixture is shared and stays as committed.
        var tree = Directory.CreateTempSubdirectory().FullName;
        try
        {
            CopyDirectory(Path.Join(SqliteWebHost.TestDataPath, "Runs"), tree);

            using var database = new TempSqliteFile();
            using var rootFactory = new WebApplicationFactory<Program>();
            var factory = SqliteWebHost.Configure(rootFactory, database.Path, tree);
            using var client = factory.CreateClient();

            var before = await AdminIngest.IngestAndWaitAsync(client);
            Assert.Equal(3, before.IngestedRuns);
            Assert.Equal(4, before.IngestedSamples);

            // p0050 is sequenced only in this run, so withdrawing it withdraws that sample entirely.
            Directory.Delete(Path.Join(tree, "2024", "MiSEQ", "complete-runs", "240430_M02340_0412_ABCDE"), recursive: true);
            Directory.Delete(Path.Join(tree, "2024", "MiSEQ", "mamma-print", "240430_M02340_0412_ABCDE"), recursive: true);

            var after = await AdminIngest.IngestAndWaitAsync(client);
            Assert.Equal(2, after.IngestedRuns);

            using var scope = factory.Services.CreateScope();

            var stats = await scope.ServiceProvider
                .GetRequiredService<ISequencingStatsReader>()
                .GetSummaryAsync(default);

            // The database is a copy of the source, not the union of every source it has ever seen.
            Assert.Equal(2, stats.RunCount);
            Assert.Equal(3, stats.SampleCount);

            var samples = scope.ServiceProvider.GetRequiredService<ISampleRepository>();
            Assert.Null(await samples.GetSampleAsync(new SampleId("p0050"), default));

            // ...and the sample that survives keeps only the runs that still exist.
            var resequenced = await samples.GetSampleAsync(new SampleId("p0001"), default);
            Assert.Equal(2, resequenced!.RunSamples.Count);
            Assert.DoesNotContain(
                resequenced.RunSamples,
                runSample => runSample.RunId.Value == "240430_M02340_0412_ABCDE");
        }
        finally
        {
            Directory.Delete(tree, recursive: true);
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);

        foreach (var directory in Directory.GetDirectories(source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Join(destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
        {
            File.Copy(file, Path.Join(destination, Path.GetRelativePath(source, file)));
        }
    }
}
