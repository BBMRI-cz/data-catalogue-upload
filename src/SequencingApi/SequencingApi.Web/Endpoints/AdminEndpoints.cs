using Mediator;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Web.Http;
using SequencingApi.Web.Mapping;

namespace SequencingApi.Web.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Manual ingestion trigger for testing on a running server: runs the same pipeline the
        // weekly Quartz job runs, and returns the ingest summary. Ingestion is idempotent.
        app.MapPost("/admin/ingest", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new IngestRecordsCommand(), cancellationToken);
            return result.Match(
                ingest => Results.Ok(IngestResponseMapper.ToResponse(ingest)),
                ErrorResults.Problem);
        })
        .WithTags("admin");

        return app;
    }
}

/// <summary>
/// Response shape for <c>POST /admin/ingest</c>. Problems are reported rather than thrown, so the
/// counts and the per-record reasons are the point of the payload, not an afterthought.
/// </summary>
/// <remarks>
/// <c>error_count</c> is the length of <c>errors</c> and is not a count of records that failed: the
/// entries sit at whatever level the problem occurred, and most of them describe records that were
/// ingested regardless. Read it alongside <c>errors</c>, never as the complement of
/// <c>ingested_samples</c>.
/// </remarks>
public sealed record IngestResponse(
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
