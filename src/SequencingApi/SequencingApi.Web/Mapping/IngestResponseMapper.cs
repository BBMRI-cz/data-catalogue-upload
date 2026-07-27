using SequencingApi.Application.Abstractions.DataSource;
using SequencingApi.Application.Features.Ingest;
using SequencingApi.Web.Endpoints;

namespace SequencingApi.Web.Mapping;

/// <summary>
/// Projects an <see cref="IngestRecordsCommandResult"/> onto the <c>POST /admin/ingest</c> response
/// contract.
/// </summary>
internal static class IngestResponseMapper
{
    public static IngestResponse ToResponse(IngestRecordsCommandResult ingest) => new(
        ingest.Ingested,
        ingest.IngestedRuns,
        ingest.IngestedPanels,
        ingest.Failed,
        [.. ingest.Errors.Select(ToResponse)]);

    private static IngestErrorResponse ToResponse(RecordReadError error) =>
        new(error.Source, error.Reference, error.Reason);
}
