using Microsoft.EntityFrameworkCore;
using Uploader.Application.Abstractions;
using Uploader.Application.Features.Sync;

namespace Uploader.Infrastructure.Persistence;

/// <summary>EF Core implementation of <see cref="ISyncRunRepository"/>.</summary>
internal sealed class SyncRunRepository(UploaderDbContext context, TimeProvider timeProvider) : ISyncRunRepository
{
    public async Task FinishAsync(RunSummary summary, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var run = await context.SyncRuns.FindAsync([summary.RunId], cancellationToken);
        if (run is null)
        {
            run = new SyncRunEntity { Id = summary.RunId, StartedAt = now };
            context.SyncRuns.Add(run);
        }

        run.FinishedAt = now;
        run.ScannedCount = summary.Scanned;
        run.ChangedCount = summary.Changed;
        run.UploadedCount = summary.Uploaded;
        run.DeletedCount = summary.Deleted;
        run.SkippedCount = summary.Skipped;
        run.FailedCount = summary.Failed;

        await context.SaveChangesAsync(cancellationToken);
    }
}
