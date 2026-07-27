using BiobankApi.Application.Features.Ingest;
using BiobankApi.Web.Http;
using BiobankApi.Web.Mapping;
using Mediator;

namespace BiobankApi.Web.Endpoints;

internal static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdminEndpoints(this IEndpointRouteBuilder app)
    {
        // Manual ingestion trigger for testing on a running server: runs the same pipeline the
        // weekly Quartz job runs, and returns the ingest summary. Ingestion is idempotent.
        app.MapPost("/admin/ingest", async (ISender sender, CancellationToken cancellationToken) =>
        {
            var result = await sender.Send(new IngestExportsCommand(), cancellationToken);
            return result.Match(
                ingest => Results.Ok(IngestResponseMapper.ToResponse(ingest)),
                ErrorResults.Problem);
        })
        .WithTags("admin");

        return app;
    }
}

/// <summary>
/// Response shape for <c>POST /admin/ingest</c>. Parse and persistence failures are reported rather
/// than thrown, so the counts and the per-record reasons are the point of the payload.
/// </summary>
public sealed record IngestResponse(int Ingested, int Failed, IReadOnlyList<IngestErrorResponse> Errors);

/// <summary>One export record that could not be turned into a valid patient.</summary>
public sealed record IngestErrorResponse(string Source, string Reference, string Reason);
