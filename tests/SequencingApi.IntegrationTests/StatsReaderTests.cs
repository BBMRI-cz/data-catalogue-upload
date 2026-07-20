using SequencingApi.Domain.Samples;
using SequencingApi.Infrastructure.Persistence;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// The summary counters are computed by aggregating the tables on every call, so these tests are the
/// only thing standing between a mis-written GROUP BY and a wrong number on the stats endpoint.
/// </summary>
public sealed class StatsReaderTests : IDisposable
{
    private readonly SqliteDatabase _db = new();

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task SummaryOfAnEmptyDatabaseIsAllZeroes()
    {
        await using var context = _db.NewContext();

        var summary = await new SqlSequencingStatsReader(context).GetSummaryAsync(CancellationToken.None);

        Assert.Equal(0, summary.SampleCount);
        Assert.Equal(0, summary.SamplesWithReads);
        Assert.Equal(0, summary.SamplesWithAnalysis);
        Assert.Equal(0, summary.ResequencedSampleCount);
        Assert.Equal(0, summary.RunSampleCount);
        Assert.Equal(0, summary.RunCount);
        Assert.Equal(0, summary.PanelCount);
        Assert.Equal(0, summary.SamplesWithUnresolvedPanel);
        Assert.Null(summary.FirstRunDate);
        Assert.Null(summary.LastRunDate);
    }

    [Fact]
    public async Task SummaryCountsSamplesRunsAndPanels()
    {
        await using var context = _db.NewContext();
        await SeedAsync(context);

        var summary = await new SqlSequencingStatsReader(context).GetSummaryAsync(CancellationToken.None);

        Assert.Equal(3, summary.SampleCount);

        // resequenced: sequenced in two runs. analysed-only: one run, with an analysis.
        // no-reads: a known sample whose folder held nothing at all.
        Assert.Equal(4, summary.RunSampleCount);
        Assert.Equal(2, summary.SamplesWithReads);
        Assert.Equal(1, summary.SamplesWithAnalysis);
        Assert.Equal(1, summary.ResequencedSampleCount);

        // Only the analysed run resolved a panel, so the other two samples are unresolved — and a
        // sample with no run at all counts as unresolved too.
        Assert.Equal(2, summary.SamplesWithUnresolvedPanel);

        Assert.Equal(2, summary.RunCount);
        Assert.Equal(1, summary.PanelCount);
        Assert.Equal(new DateOnly(2024, 1, 4), summary.FirstRunDate);
        Assert.Equal(new DateOnly(2024, 4, 30), summary.LastRunDate);
    }

    [Fact]
    public async Task UndatedRunsDoNotHideTheDateRange()
    {
        await using var context = _db.NewContext();
        var runs = new SqlSequencingRunRepository(context);
        await runs.SaveRunsAsync(
            [
                SequencingRunAggregateWithoutDate(),
                SequencingFixtures.FullRun(),
            ],
            CancellationToken.None);

        var summary = await new SqlSequencingStatsReader(context).GetSummaryAsync(CancellationToken.None);

        Assert.Equal(2, summary.RunCount);
        Assert.Equal(new DateOnly(2024, 1, 4), summary.FirstRunDate);
        Assert.Equal(new DateOnly(2024, 1, 4), summary.LastRunDate);
    }

    private static Domain.Runs.SequencingRunAggregate SequencingRunAggregateWithoutDate() =>
        Domain.Runs.SequencingRunAggregate.Create("230101_N0000000_0000_0000000000").Value;

    private static async Task SeedAsync(SequencingDbContext context)
    {
        var samples = new SqlSampleRepository(context);

        // Sequenced twice, analysed once, with a resolved panel on the analysed run.
        await samples.SaveSamplesAsync([SequencingFixtures.FullSample("mmci_predictive_resequenced")], CancellationToken.None);

        // Sequenced once, reads only, no panel.
        await samples.SaveSamplesAsync(
            [
                SampleAggregate.Create(
                    "mmci_predictive_reads_only",
                    idScheme: "mmci_predictive",
                    runSamples: [SequencingFixtures.ReadsOnlyRunSample()]).Value,
            ],
            CancellationToken.None);

        // Known, but its sample folder held nothing — a real and common state in the corpus.
        await samples.SaveSamplesAsync(
            [
                SampleAggregate.Create(
                    "mmci_predictive_empty",
                    idScheme: "mmci_predictive",
                    runSamples: [RunSample.Create(SequencingFixtures.PrimaryRunId).Value]).Value,
            ],
            CancellationToken.None);

        await new SqlSequencingRunRepository(context).SaveRunsAsync(
            [
                SequencingFixtures.FullRun(),
                SequencingRunLate(),
            ],
            CancellationToken.None);

        await new SqlPanelRepository(context).SavePanelsAsync(
            [SequencingFixtures.FullPanel()],
            CancellationToken.None);
    }

    private static Domain.Runs.SequencingRunAggregate SequencingRunLate() =>
        Domain.Runs.SequencingRunAggregate.Create(
            SequencingFixtures.SecondaryRunId,
            runDate: new DateOnly(2024, 4, 30)).Value;
}
