using Uploader.Application.Abstractions;
using Uploader.Application.Features.Sync;
using Uploader.Infrastructure.Persistence;
using Xunit;

namespace Uploader.IntegrationTests;

/// <summary>
/// The <c>sync_run</c> table is what <c>GET /admin/sync</c> reports from, so a run has to be
/// readable from the moment it starts, and its finish has to land on the same row.
/// </summary>
public sealed class SyncRunRepositoryTests
{
    private static readonly DateTimeOffset Started = new(2026, 10, 26, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task AStartedRunReadsBackAsUnfinished()
    {
        using var db = new SqliteDatabase();
        var clock = new SettableClock(Started);
        await new SyncRunRepository(db.NewContext(), clock).StartAsync("run-1", default);

        var latest = await new SyncRunRepository(db.NewContext(), clock).GetLatestAsync(default);

        Assert.NotNull(latest);
        Assert.Equal("run-1", latest.RunId);
        Assert.Equal(Started, latest.StartedAt);
        Assert.Null(latest.FinishedAt);
    }

    [Fact]
    public async Task BeforeTheFirstRunThereIsNothingToReport()
    {
        using var db = new SqliteDatabase();

        var latest = await new SyncRunRepository(db.NewContext(), TimeProvider.System).GetLatestAsync(default);

        Assert.Null(latest);
    }

    [Fact]
    public async Task TheLatestIsTheLastToStartNotTheLastToFinish()
    {
        // A run still going is newer than one that started before it, however recently that one
        // finished: GET /admin/sync has to show the run in progress.
        using var db = new SqliteDatabase();
        var clock = new SettableClock(Started);
        var repository = new SyncRunRepository(db.NewContext(), clock);
        await repository.StartAsync("older", default);
        clock.Now = Started.AddMinutes(1);
        await repository.StartAsync("newer", default);
        clock.Now = Started.AddMinutes(2);
        await repository.FinishAsync(new RunCatalogueSyncCommandResult("older"), default);

        var latest = await new SyncRunRepository(db.NewContext(), clock).GetLatestAsync(default);

        Assert.Equal("newer", latest!.RunId);
        Assert.Null(latest.FinishedAt);
    }

    [Fact]
    public async Task AFinishedRunKeepsItsStartAndCarriesEveryCountAndItsFailure()
    {
        using var db = new SqliteDatabase();
        var clock = new SettableClock(Started);
        var repository = new SyncRunRepository(db.NewContext(), clock);
        await repository.StartAsync("run-1", default);

        // Distinct values, so a dropped or transposed field cannot pass.
        clock.Now = Started.AddMinutes(18);
        await repository.FinishAsync(
            new RunCatalogueSyncCommandResult("run-1")
            {
                Scanned = 1,
                Changed = 2,
                Uploaded = 3,
                Deleted = 4,
                Skipped = 5,
                Failed = 6,
                SourceUnavailable = 7,
                Failure = "Cannot list patients: biobank is unavailable",
            },
            default);

        var latest = await new SyncRunRepository(db.NewContext(), clock).GetLatestAsync(default);

        Assert.Equal(
            new SyncRun(
                "run-1",
                Started,
                Started.AddMinutes(18),
                "Cannot list patients: biobank is unavailable",
                Scanned: 1,
                Changed: 2,
                Uploaded: 3,
                Deleted: 4,
                Skipped: 5,
                Failed: 6,
                SourceUnavailable: 7),
            latest);
    }
}
