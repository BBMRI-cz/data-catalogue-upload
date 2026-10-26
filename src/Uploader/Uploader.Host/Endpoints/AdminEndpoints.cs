using Mediator;
using Uploader.Application.Features.Sync;
using Uploader.Host.Http;
using Uploader.Host.Mapping;
using Uploader.Host.Scheduling;

namespace Uploader.Host.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Manual sync trigger: starts the same sync the schedule runs, in the background, and returns
        // at once - a sync takes from minutes to hours. 409 while one is already running.
        app.MapPost("/admin/sync", (SyncRunner runner) =>
            runner.TryStart() ? Results.Accepted() : Results.Conflict())
        .WithTags("admin");

        // Whether a sync is running, and how the latest one went - including one still in progress
        // (no finished_at yet) or one that never finished (no finished_at, and nothing running).
        app.MapGet("/admin/sync", async (SyncRunner runner, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetLatestSyncRunQuery(), cancellationToken);
            return result.Match(
                latest => Results.Ok(SyncStatusResponseMapper.ToResponse(runner.IsRunning, latest)),
                ErrorResults.Problem);
        })
        .WithTags("admin");

        return app;
    }
}

/// <summary>Response shape for <c>GET /admin/sync</c>.</summary>
public sealed record SyncStatusResponse(bool Running, SyncRunResponse? LastRun);

/// <summary>
/// One sync run. <c>finished_at</c> is null while it runs, and stays null if it never finished;
/// <c>failure</c> says why a run ended before syncing anything.
/// </summary>
public sealed record SyncRunResponse(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Failure,
    int Scanned,
    int Changed,
    int Uploaded,
    int Deleted,
    int Skipped,
    int Failed,
    int SourceUnavailable);
