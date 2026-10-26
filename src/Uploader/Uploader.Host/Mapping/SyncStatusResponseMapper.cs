using Uploader.Application.Abstractions;
using Uploader.Application.Features.Sync;
using Uploader.Host.Endpoints;

namespace Uploader.Host.Mapping;

/// <summary>Projects the latest sync run onto the <c>GET /admin/sync</c> response contract.</summary>
internal static class SyncStatusResponseMapper
{
    public static SyncStatusResponse ToResponse(bool running, GetLatestSyncRunQueryResult latest) =>
        new(running, latest.LastRun is { } run ? ToResponse(run) : null);

    private static SyncRunResponse ToResponse(SyncRun run) => new(
        run.RunId,
        run.StartedAt,
        run.FinishedAt,
        run.Failure,
        run.Scanned,
        run.Changed,
        run.Uploaded,
        run.Deleted,
        run.Skipped,
        run.Failed,
        run.SourceUnavailable);
}
