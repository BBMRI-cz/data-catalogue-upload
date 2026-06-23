using Uploader.Application.Abstractions;
using Uploader.Application.Features.Sync;
using Uploader.Infrastructure.Persistence.Entities;

namespace Uploader.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="ISyncRunRepository"/>.</summary>
internal sealed class SyncRunRepository(UploaderDbContext context, TimeProvider timeProvider) : ISyncRunRepository
{
    public async Task FinishAsync(RunCatalogueSyncCommandResult result, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var run = await context.SyncRuns.FindAsync([result.RunId], cancellationToken);
        if (run is null)
        {
            run = new SyncRunEntity { Id = result.RunId, StartedAt = now };
            context.SyncRuns.Add(run);
        }

        run.FinishedAt = now;
        run.ScannedCount = result.Scanned;
        run.ChangedCount = result.Changed;
        run.UploadedCount = result.Uploaded;
        run.DeletedCount = result.Deleted;
        run.SkippedCount = result.Skipped;
        run.FailedCount = result.Failed;

        await context.SaveChangesAsync(cancellationToken);
    }
}
