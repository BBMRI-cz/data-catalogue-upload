using Microsoft.EntityFrameworkCore;
using SequencingApi.Domain;
using SequencingApi.Domain.Common;
using SequencingApi.Domain.Samples;
using SequencingApi.Infrastructure.Persistence;
using Xunit;

namespace SequencingApi.IntegrationTests;

public sealed class RepositoryTests : IDisposable
{
    private readonly SqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SaveAndGetRoundTripsFullSample()
    {
        await using var context = _db.NewContext();
        var repository = new SqlSampleRepository(context);
        var sample = SequencingFixtures.FullSample();

        var failures = await repository.SaveSamplesAsync([sample], CancellationToken.None);
        Assert.Empty(failures);

        var loaded = await repository.GetSampleAsync(sample.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("mmci_predictive_0001", loaded.Id.Value);
        Assert.Equal("mmci_predictive", loaded.IdScheme);
        Assert.Equal("patient-4711", loaded.PredictiveNumber);
        Assert.Equal(2, loaded.RunSamples.Count);
        Assert.True(loaded.HasAnalysis);

        var analysed = loaded.RunSamples.Single(run => run.RunId.Value == SequencingFixtures.PrimaryRunId);
        Assert.Equal(2, analysed.Files.Count);
        Assert.Equal(SequencingFixtures.PanelId, analysed.LibraryPreparation!.PanelId!.Value.Value);
        Assert.Equal(812.5, analysed.Analyses.Single().Quality!.AverageCoverage);
        Assert.Equal(2, analysed.Analyses.Single().Files.Count);

        var readsOnly = loaded.RunSamples.Single(run => run.RunId.Value == SequencingFixtures.SecondaryRunId);
        Assert.Null(readsOnly.LibraryPreparation);
        Assert.Empty(readsOnly.Analyses);
    }

    [Fact]
    public async Task GetSampleReturnsNullForUnknownSample()
    {
        await using var context = _db.NewContext();
        var repository = new SqlSampleRepository(context);

        Assert.Null(await repository.GetSampleAsync(new SampleId("nope"), CancellationToken.None));
    }

    [Fact]
    public async Task GetSamplesByPredictiveNumberReturnsTheWholeSubtree()
    {
        // The lookup the uploader arrives with. It must load exactly what GetSampleAsync loads - an
        // Include missing here would serve a sample with its analyses or files silently dropped.
        await using var context = _db.NewContext();
        var repository = new SqlSampleRepository(context);
        await repository.SaveSamplesAsync([SequencingFixtures.FullSample()], CancellationToken.None);

        await using var readContext = _db.NewContext();
        var loaded = await new SqlSampleRepository(readContext)
            .GetSamplesByPredictiveNumberAsync("patient-4711", CancellationToken.None);

        var sample = Assert.Single(loaded);
        Assert.Equal("mmci_predictive_0001", sample.Id.Value);
        Assert.Equal(2, sample.RunSamples.Count);

        var analysed = sample.RunSamples.Single(run => run.RunId.Value == SequencingFixtures.PrimaryRunId);
        Assert.Equal(2, analysed.Files.Count);
        Assert.Equal(SequencingFixtures.PanelId, analysed.LibraryPreparation!.PanelId!.Value.Value);

        var analysis = Assert.Single(analysed.Analyses);
        Assert.Equal(2, analysis.Files.Count);
        Assert.Equal(812.5, analysis.Quality!.AverageCoverage);
    }

    [Fact]
    public async Task GetSamplesByPredictiveNumberReturnsEveryMatchingSample()
    {
        // The predictive number is not unique: the same subject can be sampled more than once, and
        // returning only the first would silently hide sequencing the uploader is entitled to.
        await using var context = _db.NewContext();
        var repository = new SqlSampleRepository(context);
        await repository.SaveSamplesAsync(
            [
                SequencingFixtures.FullSample("mmci_predictive_0001"),
                SequencingFixtures.FullSample("mmci_predictive_0002"),
                SampleAggregate.Create(
                    "mmci_predictive_0003",
                    idScheme: "mmci_predictive",
                    predictiveNumber: "patient-9999").Value,
            ],
            CancellationToken.None);

        await using var readContext = _db.NewContext();
        var loaded = await new SqlSampleRepository(readContext)
            .GetSamplesByPredictiveNumberAsync("patient-4711", CancellationToken.None);

        Assert.Equal(
            ["mmci_predictive_0001", "mmci_predictive_0002"],
            loaded.Select(sample => sample.Id.Value));
    }

    [Theory]
    [InlineData("patient-0000")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSamplesByPredictiveNumberFindsNothingForAnUnknownOrBlankNumber(string predictiveNumber)
    {
        // A sample with no predictive number is one the mapping table did not cover. A blank query
        // must not match those - it would hand the uploader another patient's sequencing.
        await using var context = _db.NewContext();
        await new SqlSampleRepository(context).SaveSamplesAsync(
            [
                SequencingFixtures.FullSample(),
                SampleAggregate.Create("mmci_predictive_0009", idScheme: "mmci_predictive").Value,
            ],
            CancellationToken.None);

        await using var readContext = _db.NewContext();
        Assert.Empty(await new SqlSampleRepository(readContext)
            .GetSamplesByPredictiveNumberAsync(predictiveNumber, CancellationToken.None));
    }

    [Fact]
    public async Task ResavingASampleReplacesItsChildrenRatherThanDuplicatingThem()
    {
        await using var context = _db.NewContext();
        var repository = new SqlSampleRepository(context);
        var sample = SequencingFixtures.FullSample();

        await repository.SaveSamplesAsync([sample], CancellationToken.None);
        await repository.SaveSamplesAsync([sample], CancellationToken.None);

        var loaded = await repository.GetSampleAsync(sample.Id, CancellationToken.None);
        Assert.Equal(2, loaded!.RunSamples.Count);

        // The cascade has to reach every level, or a re-ingest silently grows the tables.
        await using var probe = _db.NewContext();
        Assert.Equal(1, await probe.Samples.CountAsync());
        Assert.Equal(2, await probe.RunSamples.CountAsync());
        Assert.Equal(1, await probe.Analyses.CountAsync());
        Assert.Equal(5, await probe.SequencingFiles.CountAsync());
        Assert.Equal(1, await probe.LibraryPreparations.CountAsync());
        Assert.Equal(1, await probe.QualityMetrics.CountAsync());
    }

    [Fact]
    public async Task AbsentValueObjectsAreAbsentRowsRatherThanEmptyOnes()
    {
        // The 0..1 value objects own a table keyed on their owner, so "not recorded" is the absence
        // of a row - there is no all-null row to mistake for a real one.
        await using var context = _db.NewContext();
        await new SqlSampleRepository(context)
            .SaveSamplesAsync([SequencingFixtures.FullSample()], CancellationToken.None);

        await using var probe = _db.NewContext();

        // Two run samples, but only the analysed one had a library preparation.
        Assert.Equal(2, await probe.RunSamples.CountAsync());
        var library = Assert.Single(await probe.LibraryPreparations.AsNoTracking().ToListAsync());
        Assert.Equal(SequencingFixtures.PanelId, library.PanelId);

        var quality = Assert.Single(await probe.QualityMetrics.AsNoTracking().ToListAsync());
        Assert.Equal(812.5, quality.AverageCoverage);
    }

    [Fact]
    public async Task DeletingASampleCascadesToItsValueObjectTables()
    {
        await using var context = _db.NewContext();
        var repository = new SqlSampleRepository(context);
        await repository.SaveSamplesAsync([SequencingFixtures.FullSample()], CancellationToken.None);

        // Re-saving without the analysed run has to take the library preparation and the quality
        // metrics with it - they hang two levels below the sample.
        await repository.SaveSamplesAsync(
            [
                SampleAggregate.Create(
                    "mmci_predictive_0001",
                    idScheme: "mmci_predictive",
                    runSamples: [SequencingFixtures.ReadsOnlyRunSample()]).Value,
            ],
            CancellationToken.None);

        await using var probe = _db.NewContext();
        Assert.Equal(0, await probe.LibraryPreparations.CountAsync());
        Assert.Equal(0, await probe.QualityMetrics.CountAsync());
        Assert.Equal(0, await probe.Analyses.CountAsync());
    }

    [Fact]
    public async Task RunReadsKeepTheirOrderAcrossARoundTrip()
    {
        // The read structure is template/index/template; if the order were lost, the expected-FASTQ
        // derivation built on it would silently start counting the wrong reads.
        await using var context = _db.NewContext();
        var repository = new SqlSequencingRunRepository(context);
        var run = SequencingFixtures.FullRun();
        await repository.SaveRunsAsync([run], CancellationToken.None);

        var loaded = await repository.GetRunAsync(run.Id, CancellationToken.None);

        Assert.Equal(run.Reads, loaded!.Reads);
        Assert.Equal([false, true, false], loaded.Reads.Select(read => read.IsIndexedRead));
        Assert.Equal(2, loaded.TemplateReadCount);

        await using var probe = _db.NewContext();
        Assert.Equal(3, await probe.RunReads.CountAsync());
    }

    [Fact]
    public async Task ResavingASampleAppliesTheNewShape()
    {
        await using var context = _db.NewContext();
        var repository = new SqlSampleRepository(context);

        await repository.SaveSamplesAsync([SequencingFixtures.FullSample()], CancellationToken.None);

        // The same sample, now known only from its reads-only run.
        var shrunk = SampleAggregate.Create(
            "mmci_predictive_0001",
            idScheme: "mmci_predictive",
            runSamples: [SequencingFixtures.ReadsOnlyRunSample()]).Value;
        await repository.SaveSamplesAsync([shrunk], CancellationToken.None);

        var loaded = await repository.GetSampleAsync(shrunk.Id, CancellationToken.None);
        Assert.Equal(SequencingFixtures.SecondaryRunId, Assert.Single(loaded!.RunSamples).RunId.Value);
        Assert.Null(loaded.PredictiveNumber);
        Assert.False(loaded.HasAnalysis);

        await using var probe = _db.NewContext();
        Assert.Equal(0, await probe.Analyses.CountAsync());
    }

    [Fact]
    public async Task SampleSavesEvenWhenItsRunAndPanelAreNotStoredYet()
    {
        // Runs and panels are separate aggregate roots referenced by identity only. If either were a
        // real foreign key, ingest order would become load-bearing and this would fail.
        await using var context = _db.NewContext();
        var repository = new SqlSampleRepository(context);

        var failures = await repository.SaveSamplesAsync(
            [SequencingFixtures.FullSample()],
            CancellationToken.None);

        Assert.Empty(failures);
        await using var probe = _db.NewContext();
        Assert.Equal(0, await probe.SequencingRuns.CountAsync());
        Assert.Equal(0, await probe.Panels.CountAsync());
    }

    [Fact]
    public async Task SavingManySamplesKeepsThemApart()
    {
        await using var context = _db.NewContext();
        var repository = new SqlSampleRepository(context);
        var samples = Enumerable.Range(1, 25)
            .Select(index => SequencingFixtures.FullSample($"mmci_predictive_{index:D4}"))
            .ToList();

        var failures = await repository.SaveSamplesAsync(samples, CancellationToken.None);

        Assert.Empty(failures);
        var loaded = await repository.GetSampleAsync(new SampleId("mmci_predictive_0013"), CancellationToken.None);
        Assert.Equal(2, loaded!.RunSamples.Count);

        await using var probe = _db.NewContext();
        Assert.Equal(25, await probe.Samples.CountAsync());
    }

    [Fact]
    public async Task SaveAndGetRoundTripsRun()
    {
        await using var context = _db.NewContext();
        var repository = new SqlSequencingRunRepository(context);
        var run = SequencingFixtures.FullRun();

        Assert.Empty(await repository.SaveRunsAsync([run], CancellationToken.None));
        var loaded = await repository.GetRunAsync(run.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("M02340", loaded.InstrumentId);
        Assert.Equal(new DateOnly(2024, 1, 4), loaded.RunDate);
        Assert.Equal(run.Reads, loaded.Reads);
        Assert.Equal(94.7, loaded.PercentageQ30);
    }

    [Fact]
    public async Task ResavingARunIsIdempotent()
    {
        await using var context = _db.NewContext();
        var repository = new SqlSequencingRunRepository(context);

        await repository.SaveRunsAsync([SequencingFixtures.FullRun()], CancellationToken.None);
        await repository.SaveRunsAsync([SequencingFixtures.FullRun()], CancellationToken.None);

        await using var probe = _db.NewContext();
        Assert.Equal(1, await probe.SequencingRuns.CountAsync());
        Assert.Equal(3, await probe.RunReads.CountAsync());
    }

    [Fact]
    public async Task GetRunReturnsNullForUnknownRun()
    {
        await using var context = _db.NewContext();
        var repository = new SqlSequencingRunRepository(context);

        Assert.Null(await repository.GetRunAsync(new SequencingRunId("NOPE"), CancellationToken.None));
    }

    [Fact]
    public async Task GetRunsReturnsOnlyTheRequestedRunsThatExist()
    {
        // Samples name their runs by identity with no foreign key, so a dangling reference is legal:
        // an unknown id has to come back absent rather than as an error or a null in the list.
        await using var context = _db.NewContext();
        var repository = new SqlSequencingRunRepository(context);
        await repository.SaveRunsAsync(
            [SequencingFixtures.FullRun(), SequencingFixtures.FullRun(SequencingFixtures.SecondaryRunId)],
            CancellationToken.None);

        await using var readContext = _db.NewContext();
        var loaded = await new SqlSequencingRunRepository(readContext).GetRunsAsync(
            [new SequencingRunId(SequencingFixtures.PrimaryRunId), new SequencingRunId("NOPE")],
            CancellationToken.None);

        var run = Assert.Single(loaded);
        Assert.Equal(SequencingFixtures.PrimaryRunId, run.Id.Value);

        // The reads are what make this worth a batch read rather than a projection.
        Assert.Equal([false, true, false], run.Reads.Select(read => read.IsIndexedRead));
    }

    [Fact]
    public async Task GetRunsReturnsNothingForNoIds()
    {
        await using var context = _db.NewContext();
        var repository = new SqlSequencingRunRepository(context);
        await repository.SaveRunsAsync([SequencingFixtures.FullRun()], CancellationToken.None);

        Assert.Empty(await repository.GetRunsAsync([], CancellationToken.None));
    }

    [Fact]
    public async Task SaveAndGetRoundTripsPanel()
    {
        await using var context = _db.NewContext();
        var repository = new SqlPanelRepository(context);
        var panel = SequencingFixtures.FullPanel();

        Assert.Empty(await repository.SavePanelsAsync([panel], CancellationToken.None));
        var loaded = await repository.GetPanelAsync(panel.Id, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("HyperCap MOP", loaded.Name);
        Assert.Equal(["BRCA1", "BRCA2", "TP53"], loaded.Genes);
        Assert.Equal(new DateOnly(2025, 12, 31), loaded.AvailableTo);
    }

    [Fact]
    public async Task ResavingAPanelIsIdempotent()
    {
        await using var context = _db.NewContext();
        var repository = new SqlPanelRepository(context);

        await repository.SavePanelsAsync([SequencingFixtures.FullPanel()], CancellationToken.None);
        await repository.SavePanelsAsync([SequencingFixtures.FullPanel()], CancellationToken.None);

        await using var probe = _db.NewContext();
        Assert.Equal(1, await probe.Panels.CountAsync());
    }

    [Fact]
    public async Task GetPanelsReturnsOnlyTheRequestedPanelsThatExist()
    {
        // Same contract as the runs: a library preparation that failed to match a panel leaves a
        // dangling id, which is routine and must not turn a lookup into a failure.
        await using var context = _db.NewContext();
        var repository = new SqlPanelRepository(context);
        await repository.SavePanelsAsync(
            [SequencingFixtures.FullPanel(), SequencingFixtures.FullPanel("hypercap-v2")],
            CancellationToken.None);

        await using var readContext = _db.NewContext();
        var loaded = await new SqlPanelRepository(readContext).GetPanelsAsync(
            [new PanelId(SequencingFixtures.PanelId), new PanelId("nope")],
            CancellationToken.None);

        var panel = Assert.Single(loaded);
        Assert.Equal(SequencingFixtures.PanelId, panel.Id.Value);
        Assert.Equal(["BRCA1", "BRCA2", "TP53"], panel.Genes);
    }

    [Fact]
    public async Task GetPanelsReturnsNothingForNoIds()
    {
        await using var context = _db.NewContext();
        var repository = new SqlPanelRepository(context);
        await repository.SavePanelsAsync([SequencingFixtures.FullPanel()], CancellationToken.None);

        Assert.Empty(await repository.GetPanelsAsync([], CancellationToken.None));
    }

    [Fact]
    public async Task EveryStoredFileHasExactlyOneOwner()
    {
        await using var context = _db.NewContext();
        await new SqlSampleRepository(context)
            .SaveSamplesAsync([SequencingFixtures.FullSample()], CancellationToken.None);

        await using var probe = _db.NewContext();
        var files = await probe.SequencingFiles.AsNoTracking().ToListAsync();

        Assert.Equal(5, files.Count);
        Assert.All(files, file => Assert.True(
            (file.RunSampleId is null) != (file.AnalysisId is null),
            $"file {file.Path} has RunSampleId={file.RunSampleId} and AnalysisId={file.AnalysisId}"));
        Assert.Equal(2, files.Count(file => file.AnalysisId is not null));
    }

    [Fact]
    public async Task CancellationPropagatesInsteadOfBeingReportedAsRecordFailures()
    {
        // The save loops catch broadly so one bad record cannot abort a run. Cancellation must be
        // the exception to that: caught, it would be retried per record, fail again, and come back
        // as a list of invented RecordReadErrors while the caller believes the run merely had bad
        // data - the run would look completed rather than cancelled.
        await using var context = _db.NewContext();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SqlSampleRepository(context)
                .SaveSamplesAsync([SequencingFixtures.FullSample()], cancelled.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SqlSequencingRunRepository(context)
                .SaveRunsAsync([SequencingFixtures.FullRun()], cancelled.Token));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new SqlPanelRepository(context)
                .SavePanelsAsync([SequencingFixtures.FullPanel()], cancelled.Token));
    }

    [Fact]
    public async Task AFileWithBothOwnersIsRejectedByTheDatabase()
    {
        // Proves the single-owner check constraint is actually live rather than silently dropped -
        // without this, the assertion above would only ever be testing the mapper's good behaviour.
        await using var context = _db.NewContext();
        await new SqlSampleRepository(context)
            .SaveSamplesAsync([SequencingFixtures.FullSample()], CancellationToken.None);

        await using var probe = _db.NewContext();
        var runSampleId = probe.RunSamples.Select(runSample => runSample.Id).First();
        var analysisId = probe.Analyses.Select(analysis => analysis.Id).First();
        probe.SequencingFiles.Add(new Infrastructure.Persistence.Entities.SequencingFileEntity
        {
            RunSampleId = runSampleId,
            AnalysisId = analysisId,
            Role = FileRole.Other,
            Path = "both/owners.txt",
        });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => probe.SaveChangesAsync());
    }
}
