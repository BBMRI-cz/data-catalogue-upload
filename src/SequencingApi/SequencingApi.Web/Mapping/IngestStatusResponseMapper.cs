using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Application.Abstractions.Repositories;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Web.Endpoints;

namespace SequencingApi.Web.Mapping;

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
        run.IngestedSamples,
        run.IngestedRuns,
        run.IngestedPanels,
        run.ErrorCount,
        [.. run.Errors.Select(ToResponse)]);

    private static IngestErrorResponse ToResponse(RecordReadError error) =>
        new(error.Source, error.Reference, error.Reason);
}
