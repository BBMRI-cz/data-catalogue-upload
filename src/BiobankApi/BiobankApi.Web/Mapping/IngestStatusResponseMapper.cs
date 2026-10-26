using BiobankApi.Application.Abstractions.Export;
using BiobankApi.Application.Abstractions.Repositories;
using BiobankApi.Application.Features.Ingest;
using BiobankApi.Web.Endpoints;

namespace BiobankApi.Web.Mapping;

/// <summary>Projects the latest ingest onto the <c>GET /admin/ingest</c> response contract.</summary>
internal static class IngestStatusResponseMapper
{
    public static IngestStatusResponse ToResponse(bool running, GetLatestIngestRunQueryResult latest) =>
        new(running, latest.LastRun is { } run ? ToResponse(run) : null);

    private static IngestRunResponse ToResponse(IngestRun run) => new(
        run.RunId,
        run.StartedAt,
        run.FinishedAt,
        run.Failure,
        run.Ingested,
        run.Failed,
        [.. run.Errors.Select(ToResponse)]);

    private static IngestErrorResponse ToResponse(ExportParseError error) =>
        new(error.Source, error.Reference, error.Reason);
}
