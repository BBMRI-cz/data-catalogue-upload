using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Infrastructure.Persistence;
using Xunit;

namespace SequencingApi.IntegrationTests;

/// <summary>
/// The <c>ingest_run</c> table is what <c>GET /admin/ingest</c> reports from, so an ingest has to
/// be readable from the moment it starts, and its outcome - counts, the reader's errors, or the
/// reason it could not run at all - has to land on the same row and survive a restart.
/// </summary>
public sealed class IngestRunRepositoryTests
{
    private static readonly DateTimeOffset Started = new(2026, 10, 25, 17, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task BeforeTheFirstIngestThereIsNothingToReport()
    {
        using var db = new SqliteDatabase();

        var latest = await new SqlIngestRunRepository(db.NewContext(), TimeProvider.System).GetLatestAsync(default);

        Assert.Null(latest);
    }

    [Fact]
    public async Task AStartedIngestReadsBackAsUnfinished()
    {
        using var db = new SqliteDatabase();
        var clock = new SettableClock(Started);
        await new SqlIngestRunRepository(db.NewContext(), clock).StartAsync("run-1", default);

        var latest = await new SqlIngestRunRepository(db.NewContext(), clock).GetLatestAsync(default);

        Assert.NotNull(latest);
        Assert.Equal("run-1", latest.RunId);
        Assert.Equal(Started, latest.StartedAt);
        Assert.Null(latest.FinishedAt);
        Assert.Null(latest.Failure);
        Assert.Empty(latest.Errors);
    }

    [Fact]
    public async Task AFinishedIngestKeepsItsStartAndCarriesEveryCountAndError()
    {
        using var db = new SqliteDatabase();
        var clock = new SettableClock(Started);
        var repository = new SqlIngestRunRepository(db.NewContext(), clock);
        await repository.StartAsync("run-1", default);

        // Distinct counts, so a dropped or transposed field cannot pass.
        clock.Now = Started.AddMinutes(40);
        RecordReadError[] errors =
        [
            new("sample_sheet", "RUN_1", "sample folder absent from the sheet"),
            new("panel", "DNA_X", "no libraries row"),
        ];
        await repository.FinishAsync(
            "run-1",
            new IngestRecordsCommandResult(IngestedSamples: 3, IngestedRuns: 4, IngestedPanels: 5, ErrorCount: 2, errors),
            default);

        var latest = await new SqlIngestRunRepository(db.NewContext(), clock).GetLatestAsync(default);

        Assert.NotNull(latest);
        Assert.Equal(Started, latest.StartedAt);
        Assert.Equal(Started.AddMinutes(40), latest.FinishedAt);
        Assert.Null(latest.Failure);
        Assert.Equal(3, latest.IngestedSamples);
        Assert.Equal(4, latest.IngestedRuns);
        Assert.Equal(5, latest.IngestedPanels);
        Assert.Equal(2, latest.ErrorCount);
        Assert.Equal(errors, latest.Errors);
    }

    [Fact]
    public async Task AnIngestThatCouldNotRunCarriesTheReason()
    {
        using var db = new SqliteDatabase();
        var clock = new SettableClock(Started);
        var repository = new SqlIngestRunRepository(db.NewContext(), clock);
        await repository.StartAsync("run-1", default);

        clock.Now = Started.AddSeconds(1);
        await repository.FailAsync("run-1", "sequencing data directory not found: /data/runs", default);

        var latest = await new SqlIngestRunRepository(db.NewContext(), clock).GetLatestAsync(default);

        Assert.NotNull(latest);
        Assert.Equal(Started.AddSeconds(1), latest.FinishedAt);
        Assert.Equal("sequencing data directory not found: /data/runs", latest.Failure);
        Assert.Equal(0, latest.IngestedSamples);
        Assert.Empty(latest.Errors);
    }

    [Fact]
    public async Task TheLatestIsTheLastToStartNotTheLastToFinish()
    {
        using var db = new SqliteDatabase();
        var clock = new SettableClock(Started);
        var repository = new SqlIngestRunRepository(db.NewContext(), clock);
        await repository.StartAsync("older", default);
        clock.Now = Started.AddMinutes(1);
        await repository.StartAsync("newer", default);
        clock.Now = Started.AddMinutes(2);
        await repository.FinishAsync("older", new IngestRecordsCommandResult(0, 0, 0, 0, []), default);

        var latest = await new SqlIngestRunRepository(db.NewContext(), clock).GetLatestAsync(default);

        Assert.Equal("newer", latest!.RunId);
        Assert.Null(latest.FinishedAt);
    }
}
