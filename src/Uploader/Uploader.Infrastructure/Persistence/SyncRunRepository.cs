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

    public async Task FinishAsync(RunCatalogueSyncCommandResult result, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var run = await _context.SyncRuns.FindAsync([result.RunId], cancellationToken);
        if (run is null)
        {
            run = new SyncRunEntity { Id = result.RunId, StartedAt = now };
            _context.SyncRuns.Add(run);
        }

        run.FinishedAt = now;
        run.ScannedCount = result.Scanned;
        run.ChangedCount = result.Changed;
        run.UploadedCount = result.Uploaded;
        run.DeletedCount = result.Deleted;
        run.SkippedCount = result.Skipped;
        run.FailedCount = result.Failed;

        await _context.SaveChangesAsync(cancellationToken);
    }
}
