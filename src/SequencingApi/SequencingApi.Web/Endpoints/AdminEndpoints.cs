using Mediator;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Web.Http;

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
            return result.Match(success => Results.Ok(success), ErrorResults.Problem);
        })
        .WithTags("admin");

        return app;
    }
}
