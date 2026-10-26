using Microsoft.EntityFrameworkCore;
using Uploader.Application.Abstractions;
using Uploader.Application.Features.Sync;
using Uploader.Infrastructure.Persistence.Entities;

namespace Uploader.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="ISyncRunRepository"/>.</summary>
internal sealed class SyncRunRepository : ISyncRunRepository
{
    private readonly UploaderDbContext _context;
    private readonly TimeProvider _timeProvider;

    public SyncRunRepository(UploaderDbContext context, TimeProvider timeProvider)
    {
        _context = context;
        _timeProvider = timeProvider;
    }

    public async Task StartAsync(string runId, CancellationToken cancellationToken)
    {
        _context.SyncRuns.Add(new SyncRunEntity { Id = runId, StartedAt = _timeProvider.GetUtcNow() });
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task FinishAsync(RunCatalogueSyncCommandResult result, CancellationToken cancellationToken)
    {
        var run = await _context.SyncRuns.FindAsync([result.RunId], cancellationToken)
            ?? throw new InvalidOperationException($"Sync run {result.RunId} finished without being started.");

        run.FinishedAt = _timeProvider.GetUtcNow();
        run.Failure = result.Failure;
        run.ScannedCount = result.Scanned;
        run.ChangedCount = result.Changed;
        run.UploadedCount = result.Uploaded;
        run.DeletedCount = result.Deleted;
        run.SkippedCount = result.Skipped;
        run.FailedCount = result.Failed;
        run.SourceUnavailableCount = result.SourceUnavailable;

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<SyncRun?> GetLatestAsync(CancellationToken cancellationToken)
    {
        // ponytail: ordered in memory because SQLite, the test engine, cannot ORDER BY a
        // DateTimeOffset. A run is a few rows a week, so reading them all stays cheap; order in SQL
        // (via a sortable column) if the table ever grows large.
        var runs = await _context.SyncRuns.AsNoTracking().ToListAsync(cancellationToken);
        var run = runs.MaxBy(entity => entity.StartedAt);

        return run is null ? null : ToSyncRun(run);
    }

    private static SyncRun ToSyncRun(SyncRunEntity run) => new(
        run.Id,
        run.StartedAt,
        run.FinishedAt,
        run.Failure,
        run.ScannedCount,
        run.ChangedCount,
        run.UploadedCount,
        run.DeletedCount,
        run.SkippedCount,
        run.FailedCount,
        run.SourceUnavailableCount);
}
