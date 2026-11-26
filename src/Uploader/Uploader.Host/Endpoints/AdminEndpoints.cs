using System.Text.Json.Serialization;
using Mediator;
using Uploader.Application.Features.Ontology;
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

        // The values the latest runs could not fill an ontology field from, for the ontology-mapping
        // student. Read from the table, so it answers between runs and before the first one.
        app.MapGet("/admin/ontology/unmapped", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetUnmappedValuesQuery(), cancellationToken);
            return result.Match(
                list => Results.Ok(UnmappedValuesResponseMapper.ToResponse(list)),
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
/// <c>failure</c> says why a run ended before syncing anything; <c>unmapped</c> is how many distinct
/// values it left unmapped, which <c>GET /admin/ontology/unmapped</c> lists.
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
    int SourceUnavailable,
    int Unmapped);

/// <summary>
/// One item of <c>GET /admin/ontology/unmapped</c>, in the exchange format proposed to the
/// ontology-mapping student: the catalogue field as table and column, the value exactly as the source
/// served it, the context it was looked up in, how many catalogue rows held it in the run that last
/// met it, and when a run first did. It never carries an identifier or a record.
/// </summary>
public sealed record UnmappedValueResponse(
    string Field,
    string Value,
    UnmappedContextResponse Context,
    int Count,
    DateTimeOffset FirstSeenAt);

/// <summary>
/// The context a value was looked up in, holding only the keys that are set: the sample type and the
/// biobank's label for a material code, and nothing - <c>{}</c> - for every other field.
/// </summary>
public sealed record UnmappedContextResponse(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? SampleType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Label);
