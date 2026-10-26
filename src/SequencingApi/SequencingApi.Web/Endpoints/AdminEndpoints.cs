using Mediator;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Web.Http;
using SequencingApi.Web.Mapping;
using SequencingApi.Web.Scheduling;

namespace SequencingApi.Web.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Manual ingestion trigger: starts the same ingest the weekly Quartz job runs, in the
        // background, and returns at once - walking the run tree takes a while. 409 while one is
        // already running. Ingestion is idempotent.
        app.MapPost("/admin/ingest", (IngestionRunner runner) =>
            runner.TryStart() ? Results.Accepted() : Results.Conflict())
        .WithTags("admin");

        // Whether an ingest is running, and how the latest one went - including one still in
        // progress (no finished_at yet) or one that never finished (no finished_at, nothing running).
        app.MapGet("/admin/ingest", async (IngestionRunner runner, ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new GetLatestIngestRunQuery(), cancellationToken);
            return result.Match(
                latest => Results.Ok(IngestStatusResponseMapper.ToResponse(runner.IsRunning, latest)),
                ErrorResults.Problem);
        })
        .WithTags("admin");

        return app;
    }
}

/// <summary>Response shape for <c>GET /admin/ingest</c>.</summary>
public sealed record IngestStatusResponse(bool Running, IngestRunResponse? LastRun);

/// <summary>
/// One ingest. <c>finished_at</c> is null while it runs, and stays null if it never finished;
/// <c>failure</c> says why an ingest could not run at all. Problems below that are reported in
/// <c>errors</c> rather than failing the ingest, so the counts and the per-record reasons are the
/// point of the payload, not an afterthought.
/// </summary>
/// <remarks>
/// <c>error_count</c> is the length of <c>errors</c> and is not a count of records that failed: the
/// entries sit at whatever level the problem occurred, and most of them describe records that were
/// ingested regardless. Read it alongside <c>errors</c>, never as the complement of
/// <c>ingested_samples</c>.
/// </remarks>
public sealed record IngestRunResponse(
    string RunId,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    string? Failure,
    int IngestedSamples,
    int IngestedRuns,
    int IngestedPanels,
    int ErrorCount,
    IReadOnlyList<IngestErrorResponse> Errors);

/// <summary>
/// One problem the reader found, naming what it is about — which may be a file, a folder, a
/// run-sample or an aggregate — and why.
/// </summary>
public sealed record IngestErrorResponse(string Source, string Reference, string Reason);
